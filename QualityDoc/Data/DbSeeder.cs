using Microsoft.EntityFrameworkCore;
using QualityDoc.Models.Domain;

namespace QualityDoc.Data;

/// <summary>Siembra datos base en SQL Server (empresas, usuarios, areas) si no existen.</summary>
public static class DbSeeder
{
    public static async Task SeedAsync(CoreDbContext db, string password)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

        // Empresas
        var sistema = await db.Empresas.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Slug == "sistema");
        if (sistema is null)
        {
            sistema = new Empresa { Nombre = "Sistema QualityDoc", Slug = "sistema" };
            db.Empresas.Add(sistema);
        }
        var demo = await db.Empresas.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Slug == "empresa-demo");
        if (demo is null)
        {
            demo = new Empresa { Nombre = "Empresa Demo S.A.", Slug = "empresa-demo" };
            db.Empresas.Add(demo);
        }
        await db.SaveChangesAsync();

        int RolId(string nombre) => db.Roles.IgnoreQueryFilters().First(r => r.Nombre == nombre).Id;

        // Usuarios (uno por rol)
        await EnsureUsuario(db, "superadmin@qualitydoc.sys", hash, "Super", "Admin",     sistema.Id, RolId(Roles.SuperAdmin));
        await EnsureUsuario(db, "admin@empresa-demo.com",    hash, "Ana",   "Garcia",    demo.Id,    RolId(Roles.Admin));
        await EnsureUsuario(db, "revisor@empresa-demo.com",  hash, "Luis",  "Martinez",  demo.Id,    RolId(Roles.Revisor));
        await EnsureUsuario(db, "creador@empresa-demo.com",  hash, "Maria", "Lopez",     demo.Id,    RolId(Roles.Creador));
        await EnsureUsuario(db, "lector@empresa-demo.com",   hash, "Carlos","Rodriguez", demo.Id,    RolId(Roles.Lector));

        // Areas de la empresa demo
        if (!await db.Areas.IgnoreQueryFilters().AnyAsync(a => a.EmpresaId == demo.Id))
        {
            db.Areas.AddRange(
                new Area { EmpresaId = demo.Id, Nombre = "Calidad",          Descripcion = "Sistema de gestion de calidad", EsGlobal = true },
                new Area { EmpresaId = demo.Id, Nombre = "Seguridad",        Descripcion = "Protocolos de seguridad e higiene", EsGlobal = true },
                new Area { EmpresaId = demo.Id, Nombre = "Recursos Humanos", Descripcion = "Politicas y procedimientos de RRHH", EsGlobal = true },
                new Area { EmpresaId = demo.Id, Nombre = "Produccion",       Descripcion = "Manuales y procedimientos de produccion", EsGlobal = true },
                new Area { EmpresaId = demo.Id, Nombre = "Mantenimiento",    Descripcion = "Planes y registros de mantenimiento", EsGlobal = false });
        }
        await db.SaveChangesAsync();
    }

    private static async Task EnsureUsuario(CoreDbContext db, string email, string hash,
        string nombre, string apellido, int empresaId, int rolId)
    {
        if (await db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == email)) return;
        db.Usuarios.Add(new Usuario
        {
            Email = email, PasswordHash = hash, Nombre = nombre, Apellido = apellido,
            EmpresaId = empresaId, RolId = rolId, Activo = true
        });
    }
}
