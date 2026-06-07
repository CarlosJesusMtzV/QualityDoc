using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Data;
using QualityDoc.Middleware;
using QualityDoc.Services.Tenant;

var builder = WebApplication.CreateBuilder(args);

// QuestPDF (generación de reportes PDF) — licencia gratuita Community.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// ── MVC ───────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// ── Contexto de tenant (empresa/rol del usuario logueado) ─────
builder.Services.AddScoped<ITenantContext, TenantContext>();

// ── Servicios de aplicación ───────────────────────────────────
builder.Services.AddScoped<QualityDoc.Services.Auth.IAuthService, QualityDoc.Services.Auth.AuthService>();
builder.Services.AddScoped<QualityDoc.Services.Audit.IAuditService, QualityDoc.Services.Audit.AuditService>();
builder.Services.AddScoped<QualityDoc.Services.Documents.ISemVerService, QualityDoc.Services.Documents.SemVerService>();
builder.Services.AddScoped<QualityDoc.Services.Storage.IFileStorageService, QualityDoc.Services.Storage.FileStorageService>();
builder.Services.AddScoped<QualityDoc.Services.Search.IMetadataService, QualityDoc.Services.Search.NodeMetadataService>();
builder.Services.AddScoped<QualityDoc.Services.Documents.IDocumentService, QualityDoc.Services.Documents.DocumentService>();

// ── EF Core: SQL Server (nucleo) ──────────────────────────────
builder.Services.AddDbContext<CoreDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Core"),
        sql => sql.EnableRetryOnFailure()));

// ── EF Core: PostgreSQL (auditoria) ───────────────────────────
builder.Services.AddDbContext<AuditDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Audit")));

// ── Cliente del microservicio Node.js (dueño de MongoDB) ──────
builder.Services.AddHttpClient("Node", c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Services:NodeUrl"] ?? "http://localhost:3000");
    var apiKey = builder.Configuration["Services:NodeApiKey"];
    if (!string.IsNullOrEmpty(apiKey)) c.DefaultRequestHeaders.Add("x-api-key", apiKey);
    c.Timeout = TimeSpan.FromSeconds(15);
});

// ── Autenticacion por cookies ─────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Auth/Login";
        o.AccessDeniedPath = "/Auth/Denegado";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.Cookie.Name = "qd_auth";
        o.Cookie.HttpOnly = true;
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// ── Crear esquema + seed al arrancar ──────────────────────────
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    try
    {
        // Migraciones EF: crean/actualizan el esquema y aplican los datos HasData (roles).
        // Genera las migraciones una vez en la Consola del Administrador de paquetes:
        //   Add-Migration Inicial      -Context CoreDbContext
        //   Add-Migration InicialAudit -Context AuditDbContext
        // A futuro, cada cambio de modelo es solo otro Add-Migration (sin borrar datos).
        var core = sp.GetRequiredService<CoreDbContext>();
        await core.Database.MigrateAsync();

        var audit = sp.GetRequiredService<AuditDbContext>();
        await audit.Database.MigrateAsync();

        var seedPwd = builder.Configuration["Seed:AdminPassword"] ?? "QualityDoc2026!";
        var storage = sp.GetRequiredService<QualityDoc.Services.Storage.IFileStorageService>();
        await DbSeeder.SeedAsync(core, seedPwd, storage);
        // Los índices de MongoDB los crea el microservicio Node.js al arrancar.
    }
    catch (Exception ex)
    {
        sp.GetRequiredService<ILogger<Program>>()
          .LogError(ex, "Error al inicializar BDs. Verifica que SQL Server y PostgreSQL esten arriba.");
    }
}

// ── Pipeline ──────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseMiddleware<TenantMiddleware>();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
