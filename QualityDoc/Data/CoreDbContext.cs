using Microsoft.EntityFrameworkCore;
using QualityDoc.Models.Domain;
using QualityDoc.Services.Tenant;
using RoleNames = QualityDoc.Models.Domain.Roles; // alias: evita choque con el DbSet "Roles"

namespace QualityDoc.Data;

/// <summary>EF Core sobre SQL Server: nucleo del sistema.</summary>
public class CoreDbContext : DbContext
{
    private readonly ITenantContext _tenant;

    public CoreDbContext(DbContextOptions<CoreDbContext> options, ITenantContext tenant)
        : base(options)
    {
        _tenant = tenant;
    }

    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<Documento> Documentos => Set<Documento>();
    public DbSet<DocumentoVersion> DocumentoVersiones => Set<DocumentoVersion>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // ── Empresa ──
        b.Entity<Empresa>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Nombre).HasMaxLength(200);
            e.Property(x => x.Slug).HasMaxLength(100);
            e.HasQueryFilter(x => (_tenant.IgnoreTenantFilter || x.Id == _tenant.EmpresaId) && x.EliminadoEn == null);
        });

        // ── Rol (no multi-tenant) ──
        b.Entity<Rol>(e =>
        {
            e.HasIndex(x => x.Nombre).IsUnique();
            e.Property(x => x.Nombre).HasMaxLength(50);
            e.HasData(
                new Rol { Id = 1, Nombre = RoleNames.SuperAdmin,  Nivel = 0 },
                new Rol { Id = 2, Nombre = RoleNames.Admin,       Nivel = 1 },
                new Rol { Id = 3, Nombre = RoleNames.Revisor,     Nivel = 3 },
                new Rol { Id = 4, Nombre = RoleNames.Creador,     Nivel = 4 },
                new Rol { Id = 5, Nombre = RoleNames.Lector,      Nivel = 5 },
                new Rol { Id = 6, Nombre = RoleNames.Autorizador, Nivel = 2 });
        });

        // ── Usuario ──
        b.Entity<Usuario>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.PasswordHash).HasMaxLength(255);
            e.HasOne(x => x.Empresa).WithMany(x => x.Usuarios)
                .HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Rol).WithMany(x => x.Usuarios)
                .HasForeignKey(x => x.RolId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Area).WithMany()
                .HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x => (_tenant.IgnoreTenantFilter || x.EmpresaId == _tenant.EmpresaId) && x.EliminadoEn == null);
        });

        // ── Area ──
        b.Entity<Area>(e =>
        {
            e.Property(x => x.Nombre).HasMaxLength(150);
            e.HasOne(x => x.Empresa).WithMany(x => x.Areas)
                .HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x => (_tenant.IgnoreTenantFilter || x.EmpresaId == _tenant.EmpresaId) && x.EliminadoEn == null);
        });

        // ── Documento ──
        b.Entity<Documento>(e =>
        {
            e.Property(x => x.Titulo).HasMaxLength(300);
            e.HasOne(x => x.Empresa).WithMany()
                .HasForeignKey(x => x.EmpresaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Area).WithMany(x => x.Documentos)
                .HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreadoPor).WithMany()
                .HasForeignKey(x => x.CreadoPorId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x => (_tenant.IgnoreTenantFilter || x.EmpresaId == _tenant.EmpresaId) && x.EliminadoEn == null);
        });

        // ── DocumentoVersion ──
        b.Entity<DocumentoVersion>(e =>
        {
            e.Property(x => x.VersionTag).HasMaxLength(20);
            e.Property(x => x.Estado).HasMaxLength(20);
            e.Property(x => x.TipoArchivo).HasMaxLength(20);
            e.Property(x => x.NombreArchivo).HasMaxLength(300);
            e.Property(x => x.RutaArchivo).HasMaxLength(600);
            e.Property(x => x.HashSha256).HasMaxLength(64);
            e.Property(x => x.TipoRechazo).HasMaxLength(10);

            e.HasIndex(x => x.DocumentoId).HasDatabaseName("IX_Versiones_Documento");
            e.HasIndex(x => new { x.EmpresaId, x.Estado });
            // Solo una version vigente por documento (indice unico filtrado)
            e.HasIndex(x => x.DocumentoId).HasFilter("[EsVigente] = 1").IsUnique()
                .HasDatabaseName("UX_Versiones_UnaVigente");

            e.HasOne(x => x.Documento).WithMany(x => x.Versiones)
                .HasForeignKey(x => x.DocumentoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.SubidoPor).WithMany()
                .HasForeignKey(x => x.SubidoPorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.RevisadoPor).WithMany()
                .HasForeignKey(x => x.RevisadoPorId).OnDelete(DeleteBehavior.Restrict);

            e.HasQueryFilter(x => _tenant.IgnoreTenantFilter || x.EmpresaId == _tenant.EmpresaId);
        });
    }
}
