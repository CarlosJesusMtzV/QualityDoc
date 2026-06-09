using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Models.Domain;
using QualityDoc.Services.Search;
using QualityDoc.Services.Storage;

namespace QualityDoc.Data;

/// <summary>
/// Siembra datos demo: 3 empresas con 4 áreas (Calidad, Producción, RR.HH., Mantenimiento)
/// y, en cada área, Revisor/Creador/Lector (correo con código de rol). Los DOCUMENTOS provienen
/// de Data/seed/documentos_reales.json (contenido real): se reparten entre las 3 empresas, se
/// guardan en SQL Server con su texto dentro del PDF y se notifican a Node para indexarlos en MongoDB.
/// </summary>
public static class DbSeeder
{
    static readonly string[] AREAS = { "Calidad", "Produccion", "Recursos Humanos", "Mantenimiento" };
    // 16 nombres = 4 áreas × 4 roles operativos (Autorizador, Revisor, Creador, Lector) por empresa.
    static readonly string[] NOMBRES = {
        "María López","Juan Pérez","Carlos Ramírez","Ana Torres","Luis Hernández","Sofía Gómez",
        "Diego Castillo","Valeria Ruiz","Jorge Mendoza","Paola Vega","Andrés Flores","Camila Reyes",
        "Ricardo Núñez","Daniela Cruz","Fernando Ríos","Gabriela Soto"
    };
    static readonly string[] ADMINS = { "Roberto Salazar", "Patricia Núñez", "Héctor Morales" };

    static string AreaDeCodigo(string codigo) => codigo.Split('-').ElementAtOrDefault(1) switch
    {
        "CAL" => "Calidad",
        "PROD" => "Produccion",
        "RH" => "Recursos Humanos",
        "MANT" => "Mantenimiento",
        _ => "Calidad"
    };

    private class EmpInfo
    {
        public Empresa Emp = default!;
        public Dictionary<string, Area> AreaPorNombre = new();
        public Dictionary<int, Usuario> CreadorPorArea = new();
        public Dictionary<int, Usuario> RevisorPorArea = new();
        public Dictionary<int, Usuario> AutorizadorPorArea = new();
        public bool SembrarDocs;
    }

    public static async Task SeedAsync(CoreDbContext db, string password, IFileStorageService storage,
        IMetadataService meta, string? seedJsonPath)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
        int RolId(string n) => db.Roles.IgnoreQueryFilters().First(r => r.Nombre == n).Id;

        var sistema = await EnsureEmpresa(db, "Sistema QualityDoc", "sistema");
        await db.SaveChangesAsync();
        await EnsureUsuario(db, "superadmin@qualitydoc.sys", hash, "Super", "Admin", sistema.Id, RolId(Roles.SuperAdmin), null);
        await db.SaveChangesAsync();

        var negocios = new (string Nombre, string Slug)[]
        {
            ("Empresa Demo S.A.",      "empresa-demo"),
            ("Industrias del Norte",   "industrias-norte"),
            ("Construcciones del Sur", "construcciones-sur"),
        };

        var infos = new List<EmpInfo>();
        for (int e = 0; e < negocios.Length; e++)
        {
            var n = negocios[e];
            var emp = await EnsureEmpresa(db, n.Nombre, n.Slug);
            await db.SaveChangesAsync();

            foreach (var an in AREAS)
                if (!await db.Areas.IgnoreQueryFilters().AnyAsync(a => a.EmpresaId == emp.Id && a.Nombre == an))
                    db.Areas.Add(new Area { EmpresaId = emp.Id, Nombre = an, Descripcion = $"Área de {an}", EsGlobal = true });
            await db.SaveChangesAsync();

            await EnsureUsuario(db, $"{Local(ADMINS[e % ADMINS.Length], "ad")}@{n.Slug}.com", hash,
                Partir(ADMINS[e % ADMINS.Length]).Nombre, Partir(ADMINS[e % ADMINS.Length]).Apellido, emp.Id, RolId(Roles.Admin), null);
            await db.SaveChangesAsync();

            var areas = db.Areas.IgnoreQueryFilters().Where(a => a.EmpresaId == emp.Id).OrderBy(a => a.Id).ToList();
            var info = new EmpInfo { Emp = emp };
            for (int ai = 0; ai < areas.Count; ai++)
            {
                var area = areas[ai];
                info.AreaPorNombre[area.Nombre] = area;
                var roles = new[] { (Roles.Autorizador, "az"), (Roles.Revisor, "rv"), (Roles.Creador, "cr"), (Roles.Lector, "lr") };
                for (int ri = 0; ri < roles.Length; ri++)
                {
                    var nombre = NOMBRES[(ai * roles.Length + ri) % NOMBRES.Length];
                    var (nom, ape) = Partir(nombre);
                    var u = await EnsureUsuario(db, $"{Local(nombre, roles[ri].Item2)}@{n.Slug}.com", hash, nom, ape, emp.Id, RolId(roles[ri].Item1), area.Id);
                    if (ri == 0) info.AutorizadorPorArea[area.Id] = u;
                    if (ri == 1) info.RevisorPorArea[area.Id] = u;
                    if (ri == 2) info.CreadorPorArea[area.Id] = u;
                }
            }
            await db.SaveChangesAsync();
            info.SembrarDocs = !await db.Documentos.IgnoreQueryFilters().AnyAsync(d => d.EmpresaId == emp.Id);
            infos.Add(info);
        }

        // ── Documentos reales desde el JSON ──
        if (string.IsNullOrWhiteSpace(seedJsonPath) || !File.Exists(seedJsonPath)) return;
        if (!infos.Any(i => i.SembrarDocs)) return;

        List<SeedDoc> reales;
        try
        {
            var raw = await File.ReadAllTextAsync(seedJsonPath);
            reales = JsonSerializer.Deserialize<List<SeedDoc>>(raw) ?? new();
        }
        catch { return; }

        for (int idx = 0; idx < reales.Count; idx++)
        {
            var r = reales[idx];
            var info = infos[idx % infos.Count];        // reparte entre las 3 empresas
            if (!info.SembrarDocs) continue;
            if (!info.AreaPorNombre.TryGetValue(AreaDeCodigo(r.Codigo), out var area)) continue;

            var creador = info.CreadorPorArea[area.Id];
            var autorizador = info.AutorizadorPorArea[area.Id];   // quien autoriza (aprueba/rechaza)
            var creado = r.Fecha?.Date ?? DateTime.UtcNow.AddDays(-60);
            var codigo = r.Codigo;
            var paginas = r.Atributos?.Paginas ?? 0;
            var aprobadoPor = r.Atributos?.AprobadoPor ?? "—";
            var contenido = r.Contenido ?? "";
            var tags = r.Tags ?? new();

            var doc = new Documento
            {
                EmpresaId = info.Emp.Id, AreaId = area.Id, Titulo = r.Titulo ?? codigo,
                Descripcion = $"Código {codigo} · {paginas} págs. · Aprobado por {aprobadoPor}",
                CreadoPorId = creador.Id, CreadoEn = creado
            };
            db.Documentos.Add(doc);
            await db.SaveChangesAsync();

            // Versiones según el estatus del documento.
            var versiones = PlantillaReal(idx, r.Estatus, aprobadoPor);
            DocumentoVersion? principal = null;
            int k = 0;
            foreach (var v in versiones)
            {
                var tag = $"v{v.ma}.{v.me}.{v.pa}";
                var vCreado = creado.AddDays(k * 3);
                bool revisado = v.estado is EstadoVersion.Aprobado or EstadoVersion.Rechazado or EstadoVersion.Obsoleto;
                var file = storage.GuardarBytes(info.Emp.Slug, doc.Id, tag, $"{doc.Titulo} - {area.Nombre}.pdf", PdfConTexto(doc.Titulo, codigo, contenido));
                var dv = new DocumentoVersion
                {
                    DocumentoId = doc.Id, EmpresaId = info.Emp.Id,
                    VersionMayor = v.ma, VersionMenor = v.me, VersionPatch = v.pa, VersionTag = tag,
                    Estado = v.estado, EsVigente = v.vig, FueAprobada = v.aprob,
                    TipoArchivo = file.Tipo, NombreArchivo = file.Nombre, RutaArchivo = file.RutaRelativa,
                    HashSha256 = file.HashSha256, TamanioBytes = file.TamanioBytes,
                    ComentarioRevision = v.com,
                    SubidoPorId = creador.Id,
                    RevisadoPorId = revisado ? autorizador.Id : (int?)null,
                    CreadoEn = vCreado,
                    RevisadoEn = revisado ? vCreado.AddDays(1) : (DateTime?)null
                };
                db.DocumentoVersiones.Add(dv);
                if (v.principal) principal = dv;
                k++;
            }
            await db.SaveChangesAsync();

            // Indexar en MongoDB (vía Node) la versión principal, con el TEXTO real precargado.
            if (principal != null)
            {
                await meta.NotificarIndexAsync(new IndexPayload
                {
                    EmpresaId = info.Emp.Id, EmpresaSlug = info.Emp.Slug,
                    DocumentoId = doc.Id, VersionId = principal.Id, VersionTag = principal.VersionTag,
                    Titulo = doc.Titulo, Area = area.Nombre, TipoArchivo = principal.TipoArchivo,
                    Estado = principal.Estado, EsVigente = principal.EsVigente,
                    RutaArchivo = principal.RutaArchivo, NombreArchivo = principal.NombreArchivo,
                    HashSha256 = principal.HashSha256, TamanioBytes = principal.TamanioBytes,
                    SubidoPor = creador.NombreCompleto,
                    Texto = contenido,
                    Etiquetas = tags,
                    Metadatos = new Dictionary<string, string>
                    {
                        ["codigo_interno"] = codigo,
                        ["numero_paginas"] = paginas.ToString(),
                        ["aprobado_por"] = aprobadoPor
                    }
                });
            }
        }
    }

    // Historial según estatus. principal = la versión que se indexa en Mongo.
    private static List<(int ma, int me, int pa, string estado, bool vig, bool aprob, string? com, bool principal)>
        PlantillaReal(int idx, bool activo, string aprobadoPor)
    {
        if (activo)
        {
            if (idx % 3 == 0)
                return new()
                {
                    (1, 0, 0, EstadoVersion.Obsoleto, false, true, null, false),
                    (2, 0, 0, EstadoVersion.Aprobado, true,  true, $"Aprobado por {aprobadoPor}", true),
                };
            return new() { (1, 0, 0, EstadoVersion.Aprobado, true, true, $"Aprobado por {aprobadoPor}", true) };
        }
        // Inactivo: alterna entre rechazado y en revisión (sin versión vigente).
        if (idx % 2 == 0)
            return new() { (1, 1, 0, EstadoVersion.Rechazado, false, false, "Rechazado: requiere correcciones antes de publicarse.", true) };
        return new() { (1, 0, 0, EstadoVersion.EnRevision, false, false, null, true) };
    }

    private static async Task<Empresa> EnsureEmpresa(CoreDbContext db, string nombre, string slug)
    {
        var emp = await db.Empresas.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Slug == slug);
        if (emp is null) { emp = new Empresa { Nombre = nombre, Slug = slug }; db.Empresas.Add(emp); }
        return emp;
    }

    private static async Task<Usuario> EnsureUsuario(CoreDbContext db, string email, string hash,
        string nombre, string apellido, int empresaId, int rolId, int? areaId)
    {
        var u = await db.Usuarios.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Email == email);
        if (u is null)
        {
            u = new Usuario { Email = email, PasswordHash = hash, Nombre = nombre, Apellido = apellido,
                EmpresaId = empresaId, RolId = rolId, AreaId = areaId, Activo = true };
            db.Usuarios.Add(u);
        }
        return u;
    }

    private static (string Nombre, string Apellido) Partir(string completo)
    {
        var p = completo.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return p.Length >= 2 ? (p[0], string.Join(' ', p[1..])) : (completo, "");
    }

    private static string Local(string completo, string code)
    {
        var (nom, ape) = Partir(completo);
        var f = Ascii(nom.ToLowerInvariant());
        var l = Ascii(ape.Split(' ').Last().ToLowerInvariant());
        return string.IsNullOrEmpty(l) ? $"{f}.{code}" : $"{f}.{l}.{code}";
    }

    // PDF de 1 página con el título, código y el texto real (envuelto). Sirve para vista previa/descarga;
    // el texto completo se manda a Mongo aparte para búsqueda/IA.
    private static byte[] PdfConTexto(string titulo, string codigo, string texto)
    {
        var lineas = new List<string> { Ascii(titulo), Ascii(codigo), "" };
        foreach (var par in Ascii(texto).Replace("\r", "").Split('\n'))
            foreach (var w in Envolver(par, 95)) lineas.Add(w);
        if (lineas.Count > 52) lineas = lineas.Take(52).ToList();

        var stream = "BT /F1 10 Tf 13 TL 40 770 Td\n" +
                     string.Join("\n", lineas.Select(l => $"({l}) Tj T*")) + "\nET";

        var objs = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {stream.Length} >>\nstream\n{stream}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        };
        var sb = new StringBuilder("%PDF-1.4\n");
        var offsets = new int[objs.Length + 1];
        for (int i = 1; i <= objs.Length; i++) { offsets[i] = sb.Length; sb.Append($"{i} 0 obj\n{objs[i - 1]}\nendobj\n"); }
        int xref = sb.Length;
        sb.Append($"xref\n0 {objs.Length + 1}\n0000000000 65535 f \n");
        for (int i = 1; i <= objs.Length; i++) sb.Append(offsets[i].ToString("D10") + " 00000 n \n");
        sb.Append($"trailer\n<< /Size {objs.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    private static IEnumerable<string> Envolver(string s, int max)
    {
        var palabras = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var linea = new StringBuilder();
        foreach (var w in palabras)
        {
            if (linea.Length + w.Length + 1 > max) { yield return linea.ToString(); linea.Clear(); }
            if (linea.Length > 0) linea.Append(' ');
            linea.Append(w);
        }
        if (linea.Length > 0) yield return linea.ToString();
    }

    private static string Ascii(string s) => (s ?? "")
        .Replace('á', 'a').Replace('é', 'e').Replace('í', 'i').Replace('ó', 'o').Replace('ú', 'u').Replace('ü', 'u')
        .Replace('Á', 'A').Replace('É', 'E').Replace('Í', 'I').Replace('Ó', 'O').Replace('Ú', 'U')
        .Replace('ñ', 'n').Replace('Ñ', 'N')
        .Replace("\\", " ").Replace("(", "[").Replace(")", "]");

    // ── Mapeo del JSON de documentos reales ──
    private class SeedDoc
    {
        [JsonPropertyName("codigo_interno")] public string Codigo { get; set; } = "PRO-CAL-000";
        [JsonPropertyName("titulo")] public string? Titulo { get; set; }
        [JsonPropertyName("tags")] public List<string>? Tags { get; set; }
        [JsonPropertyName("contenido_extraido")] public string? Contenido { get; set; }
        [JsonPropertyName("atributos_especificos")] public SeedAttr? Atributos { get; set; }
        [JsonPropertyName("estatus")] public bool Estatus { get; set; }
        [JsonPropertyName("fecha_creacion")] public SeedFecha? Fecha { get; set; }
    }
    private class SeedAttr
    {
        [JsonPropertyName("numero_paginas")] public int Paginas { get; set; }
        [JsonPropertyName("aprobado_por")] public string? AprobadoPor { get; set; }
    }
    private class SeedFecha
    {
        [JsonPropertyName("$date")] public DateTime Date { get; set; }
    }
}
