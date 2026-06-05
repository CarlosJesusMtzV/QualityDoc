using Microsoft.EntityFrameworkCore;
using QualityDoc.Models.Audit;

namespace QualityDoc.Data;

/// <summary>EF Core sobre PostgreSQL: auditoria y estadisticas.</summary>
public class AuditDbContext : DbContext
{
    public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options) { }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AccessLog> AccessLogs => Set<AccessLog>();
    public DbSet<DailyStat> DailyStats => Set<DailyStat>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_logs");
            e.Property(x => x.Detalle).HasColumnType("jsonb");
            e.HasIndex(x => x.EmpresaId);
            e.HasIndex(x => new { x.EmpresaId, x.CreadoEn });
        });

        b.Entity<AccessLog>(e =>
        {
            e.ToTable("access_logs");
            e.HasIndex(x => x.EmpresaId);
            e.HasIndex(x => new { x.DocumentoId, x.VersionId });
        });

        b.Entity<DailyStat>(e =>
        {
            e.ToTable("daily_stats");
            e.HasIndex(x => new { x.EmpresaId, x.Fecha }).IsUnique();
        });
    }
}
