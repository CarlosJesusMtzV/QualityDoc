namespace QualityDoc.Models.Domain;

public class Usuario
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public int RolId { get; set; }

    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Apellido { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime? EliminadoEn { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public Empresa? Empresa { get; set; }
    public Rol? Rol { get; set; }

    public string NombreCompleto => string.IsNullOrWhiteSpace(Apellido) ? Nombre : $"{Nombre} {Apellido}";
}
