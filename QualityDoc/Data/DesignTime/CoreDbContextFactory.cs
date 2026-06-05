using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using QualityDoc.Services.Tenant;

namespace QualityDoc.Data.DesignTime;

/// <summary>
/// Permite a las herramientas de EF (Add-Migration) crear el CoreDbContext en
/// tiempo de diseño, sin arrancar la app. La cadena de conexión solo se usa para
/// Update-Database; Add-Migration no se conecta.
/// </summary>
public class CoreDbContextFactory : IDesignTimeDbContextFactory<CoreDbContext>
{
    public CoreDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseSqlServer("Server=localhost,1450;Database=QualityDocDB;User Id=sa;Password=QualityDoc@2026!;TrustServerCertificate=True")
            .Options;

        // Tenant vacío (sin HttpContext): suficiente para construir el modelo.
        return new CoreDbContext(options, new TenantContext(new HttpContextAccessor()));
    }
}
