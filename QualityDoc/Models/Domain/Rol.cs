namespace QualityDoc.Models.Domain;

public class Rol
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty; // SUPERADMIN, ADMIN, ...
    public int Nivel { get; set; }                      // 0..4

    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}
