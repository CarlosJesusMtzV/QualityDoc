using System.Text;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Models.Domain;
using QualityDoc.Services.Storage;

namespace QualityDoc.Data;

/// <summary>
/// Siembra datos demo en SQL Server: 3 empresas con sus usuarios y, por cada área,
/// 6 documentos con historial variado de versiones (aprobadas, obsoletas y rechazadas).
/// Es idempotente: si una empresa ya tiene documentos, no los vuelve a crear.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(CoreDbContext db, string password, IFileStorageService storage)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
        int RolId(string n) => db.Roles.IgnoreQueryFilters().First(r => r.Nombre == n).Id;

        // ── Empresa "sistema" + SuperAdmin global ──
        var sistema = await EnsureEmpresa(db, "Sistema QualityDoc", "sistema");
        await db.SaveChangesAsync();
        await EnsureUsuario(db, "superadmin@qualitydoc.sys", hash, "Super", "Admin", sistema.Id, RolId(Roles.SuperAdmin), null);
        await db.SaveChangesAsync();

        // ── 3 empresas de negocio, cada una con sus áreas ──
        var negocios = new (string Nombre, string Slug, string[] Areas)[]
        {
            ("Empresa Demo S.A.",      "empresa-demo",       new[] { "Calidad", "Seguridad", "Recursos Humanos" }),
            ("Industrias del Norte",   "industrias-norte",   new[] { "Calidad", "Produccion", "Mantenimiento" }),
            ("Construcciones del Sur", "construcciones-sur", new[] { "Calidad", "Seguridad", "Recursos Humanos" }),
        };

        int seq = 0;
        foreach (var n in negocios)
        {
            var emp = await EnsureEmpresa(db, n.Nombre, n.Slug);
            await db.SaveChangesAsync();

            // Áreas
            foreach (var an in n.Areas)
                if (!await db.Areas.IgnoreQueryFilters().AnyAsync(a => a.EmpresaId == emp.Id && a.Nombre == an))
                    db.Areas.Add(new Area { EmpresaId = emp.Id, Nombre = an, Descripcion = $"Área de {an}", EsGlobal = true });
            await db.SaveChangesAsync();

            // Usuarios de la empresa (Revisor/Creador/Lector ligados al área principal)
            var areaPrincipal = db.Areas.IgnoreQueryFilters().First(a => a.EmpresaId == emp.Id && a.Nombre == n.Areas[0]);
            await EnsureUsuario(db, $"admin@{n.Slug}.com", hash, "Admin", "General", emp.Id, RolId(Roles.Admin), null);
            var revisor = await EnsureUsuario(db, $"revisor@{n.Slug}.com", hash, "Revisor", "Calidad", emp.Id, RolId(Roles.Revisor), areaPrincipal.Id);
            var creador = await EnsureUsuario(db, $"creador@{n.Slug}.com", hash, "Creador", "Calidad", emp.Id, RolId(Roles.Creador), areaPrincipal.Id);
            await EnsureUsuario(db, $"lector@{n.Slug}.com", hash, "Lector", "Calidad", emp.Id, RolId(Roles.Lector), areaPrincipal.Id);
            await db.SaveChangesAsync();

            // Documentos: solo si la empresa aún no tiene
            if (await db.Documentos.IgnoreQueryFilters().AnyAsync(d => d.EmpresaId == emp.Id)) continue;

            var areas = db.Areas.IgnoreQueryFilters().Where(a => a.EmpresaId == emp.Id).ToList();
            foreach (var area in areas)
            {
                var titulos = TitulosPorArea(area.Nombre);
                for (int i = 0; i < 6; i++)
                {
                    var titulo = titulos[i];
                    var creado = DateTime.UtcNow.AddDays(-120 + seq);
                    seq++;

                    var doc = new Documento
                    {
                        EmpresaId = emp.Id,
                        AreaId = area.Id,
                        Titulo = titulo,
                        Descripcion = $"Documento de ejemplo del área {area.Nombre}.",
                        CreadoPorId = creador.Id,
                        CreadoEn = creado
                    };
                    db.Documentos.Add(doc);
                    await db.SaveChangesAsync();   // necesitamos doc.Id

                    int k = 0;
                    foreach (var v in Plantilla(i))
                    {
                        var tag = $"v{v.ma}.{v.me}.{v.pa}";
                        var vCreado = creado.AddDays(k * 4);
                        bool revisado = v.estado is EstadoVersion.Aprobado or EstadoVersion.Rechazado or EstadoVersion.Obsoleto;

                        var file = storage.GuardarBytes(emp.Slug, doc.Id, tag,
                            $"{titulo} - {area.Nombre}.pdf", MiniPdf(titulo, tag));

                        db.DocumentoVersiones.Add(new DocumentoVersion
                        {
                            DocumentoId = doc.Id,
                            EmpresaId = emp.Id,
                            VersionMayor = v.ma, VersionMenor = v.me, VersionPatch = v.pa, VersionTag = tag,
                            Estado = v.estado, EsVigente = v.vig, FueAprobada = v.aprob,
                            TipoArchivo = file.Tipo, NombreArchivo = file.Nombre, RutaArchivo = file.RutaRelativa,
                            HashSha256 = file.HashSha256, TamanioBytes = file.TamanioBytes,
                            ComentarioRevision = v.com,
                            SubidoPorId = creador.Id,
                            RevisadoPorId = revisado ? revisor.Id : (int?)null,
                            CreadoEn = vCreado,
                            RevisadoEn = revisado ? vCreado.AddDays(1) : (DateTime?)null
                        });
                        k++;
                    }
                    await db.SaveChangesAsync();
                }
            }
        }
    }

    // ── Plantillas de historial (ordenadas de más antigua a más reciente; la última es la VIGENTE) ──
    private static List<(int ma, int me, int pa, string estado, bool vig, bool aprob, string? com)> Plantilla(int i) =>
        (i % 6) switch
        {
            0 => new() { (1, 0, 0, EstadoVersion.Aprobado, true, true, null) },

            1 => new()
            {
                (1, 1, 0, EstadoVersion.Rechazado, false, false, "Rechazado: no cumple el formato establecido."),
                (1, 0, 0, EstadoVersion.Aprobado,  true,  true,  null),
            },

            2 => new()
            {
                (1, 0, 0, EstadoVersion.Obsoleto, false, true, null),
                (2, 0, 0, EstadoVersion.Aprobado, true,  true, null),
            },

            3 => new()
            {
                (1, 0, 0, EstadoVersion.Obsoleto,  false, true,  null),
                (2, 1, 0, EstadoVersion.Rechazado, false, false, "Rechazado: requiere validación del área legal."),
                (2, 0, 0, EstadoVersion.Aprobado,  true,  true,  null),
            },

            4 => new()
            {
                (1, 1, 0, EstadoVersion.Rechazado, false, false, "Rechazado: faltan firmas de autorización."),
                (1, 2, 0, EstadoVersion.Rechazado, false, false, "Rechazado: versión incompleta."),
                (1, 0, 0, EstadoVersion.Aprobado,  true,  true,  null),
            },

            _ => new()
            {
                (1, 0, 0, EstadoVersion.Obsoleto,  false, true,  null),
                (2, 0, 0, EstadoVersion.Obsoleto,  false, true,  null),
                (3, 1, 0, EstadoVersion.Rechazado, false, false, "Rechazado: cambios no aprobados por dirección."),
                (3, 0, 0, EstadoVersion.Aprobado,  true,  true,  null),
            },
        };

    private static string[] TitulosPorArea(string area) => area switch
    {
        "Calidad" => new[] { "Manual de Calidad", "Procedimiento de Auditoría Interna", "Control de Documentos", "Gestión de No Conformidades", "Política de Calidad", "Indicadores de Desempeño" },
        "Seguridad" => new[] { "Plan de Emergencias", "Protocolo de Seguridad Industrial", "Uso de Equipo de Protección", "Análisis de Riesgos", "Inspección de Extintores", "Reporte de Incidentes" },
        "Recursos Humanos" => new[] { "Reglamento Interno de Trabajo", "Política de Vacaciones", "Proceso de Contratación", "Evaluación de Desempeño", "Código de Conducta", "Plan de Capacitación" },
        "Produccion" => new[] { "Manual de Operación de Línea", "Control de Producción", "Mantenimiento Preventivo de Línea", "Especificaciones de Producto", "Procedimiento de Empaque", "Registro de Lotes" },
        "Mantenimiento" => new[] { "Plan de Mantenimiento", "Bitácora de Equipos", "Orden de Trabajo", "Inventario de Refacciones", "Calibración de Instrumentos", "Inspección Programada" },
        _ => Enumerable.Range(1, 6).Select(x => $"Documento {x} de {area}").ToArray()
    };

    private static async Task<Empresa> EnsureEmpresa(CoreDbContext db, string nombre, string slug)
    {
        var emp = await db.Empresas.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Slug == slug);
        if (emp is null)
        {
            emp = new Empresa { Nombre = nombre, Slug = slug };
            db.Empresas.Add(emp);
        }
        return emp;
    }

    private static async Task<Usuario> EnsureUsuario(CoreDbContext db, string email, string hash,
        string nombre, string apellido, int empresaId, int rolId, int? areaId)
    {
        var u = await db.Usuarios.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Email == email);
        if (u is null)
        {
            u = new Usuario
            {
                Email = email, PasswordHash = hash, Nombre = nombre, Apellido = apellido,
                EmpresaId = empresaId, RolId = rolId, AreaId = areaId, Activo = true
            };
            db.Usuarios.Add(u);
        }
        return u;
    }

    // ── Genera un PDF mínimo válido (placeholder) con texto centrado ──
    private static byte[] MiniPdf(string titulo, string tag)
    {
        string texto = Ascii($"QualityDoc - {titulo} [{tag}]");
        string stream = $"BT /F1 15 Tf 40 150 Td ({texto}) Tj ET";

        var objs = new string[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 460 220] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {stream.Length} >>\nstream\n{stream}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        };

        var sb = new StringBuilder();
        sb.Append("%PDF-1.4\n");
        var offsets = new int[objs.Length + 1];
        for (int i = 1; i <= objs.Length; i++)
        {
            offsets[i] = sb.Length;
            sb.Append($"{i} 0 obj\n{objs[i - 1]}\nendobj\n");
        }
        int xref = sb.Length;
        sb.Append($"xref\n0 {objs.Length + 1}\n");
        sb.Append("0000000000 65535 f \n");
        for (int i = 1; i <= objs.Length; i++)
            sb.Append(offsets[i].ToString("D10") + " 00000 n \n");
        sb.Append($"trailer\n<< /Size {objs.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");

        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    private static string Ascii(string s) => s
        .Replace('á', 'a').Replace('é', 'e').Replace('í', 'i').Replace('ó', 'o').Replace('ú', 'u')
        .Replace('Á', 'A').Replace('É', 'E').Replace('Í', 'I').Replace('Ó', 'O').Replace('Ú', 'U')
        .Replace('ñ', 'n').Replace('Ñ', 'N').Replace("(", "[").Replace(")", "]");
}
