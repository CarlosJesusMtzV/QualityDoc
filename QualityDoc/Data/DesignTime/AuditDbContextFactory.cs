using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QualityDoc.Data.DesignTime;

/// <summary>Fábrica de diseño para el AuditDbContext (PostgreSQL).</summary>
public class AuditDbContextFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql("Host=localhost;Port=5440;Database=qualitydoc_audit;Username=qd_admin;Password=qd_pg_2026!")
            .Options;

        return new AuditDbContext(options);
    }
}
