namespace QualityDoc.Middleware;

/// <summary>
/// Punto de extension para resolver la empresa actual por request.
/// Hoy la empresa/rol se leen de los claims en TenantContext; este middleware
/// queda como gancho para logica futura (p.ej. subdominio por tenant).
/// </summary>
public class TenantMiddleware
{
    private readonly RequestDelegate _next;
    public TenantMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context) => _next(context);
}
