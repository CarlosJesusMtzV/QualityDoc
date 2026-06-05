namespace QualityDoc.Services.Auth;

public interface IAuthService
{
    /// <summary>Valida credenciales, crea la cookie de sesión y audita. Devuelve true si entró.</summary>
    Task<bool> LoginAsync(HttpContext ctx, string email, string password);

    Task LogoutAsync(HttpContext ctx);
}
