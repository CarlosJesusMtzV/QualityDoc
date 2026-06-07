using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using QualityDoc.Data;
using QualityDoc.Models.Domain;
using QualityDoc.Services.Audit;

namespace QualityDoc.Services.Auth;

public class AuthService : IAuthService
{
    private readonly CoreDbContext _db;
    private readonly IAuditService _audit;

    public AuthService(CoreDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<bool> LoginAsync(HttpContext ctx, string email, string password)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString();

        // Sin sesión aún: omitimos el filtro multi-tenant para buscar al usuario.
        var user = await _db.Usuarios
            .IgnoreQueryFilters()
            .Include(u => u.Rol)
            .Include(u => u.Empresa)
            .FirstOrDefaultAsync(u => u.Email == email && u.Activo && u.EliminadoEn == null);

        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            await _audit.LogAsync(user?.EmpresaId ?? 0, user?.Id, email, user?.Rol?.Nombre,
                AccionAudit.LoginFallido, "Usuario", user?.Id.ToString(), null, ip);
            return false;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Email),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Rol!.Nombre),
            new("empresa_id", user.EmpresaId.ToString()),
            new("empresa_slug", user.Empresa?.Slug ?? string.Empty),
            new("area_id", user.AreaId?.ToString() ?? string.Empty),
            new("nombre", user.NombreCompleto)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });

        await _audit.LogAsync(user.EmpresaId, user.Id, user.Email, user.Rol.Nombre,
            AccionAudit.LoginOk, "Usuario", user.Id.ToString(), null, ip);
        return true;
    }

    public Task LogoutAsync(HttpContext ctx) =>
        ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
}
