namespace QualityDoc.Models.Domain;

public class Empresa
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public DateTime? EliminadoEn { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    public ICollection<Area> Areas { get; set; } = new List<Area>();
}
