using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QualityDoc.Models.ViewModels;

public class EmpresaCreateViewModel
{
    [Required, StringLength(200)] public string Nombre { get; set; } = string.Empty;
    [Required, StringLength(100)]
    [RegularExpression("^[a-z0-9-]+$", ErrorMessage = "Solo minúsculas, números y guiones.")]
    public string Slug { get; set; } = string.Empty;

    [Required, EmailAddress, Display(Name = "Correo del admin")]
    public string AdminEmail { get; set; } = string.Empty;
    [Required, Display(Name = "Nombre del admin")] public string AdminNombre { get; set; } = string.Empty;
    [Required, MinLength(8), DataType(DataType.Password), Display(Name = "Contraseña del admin")]
    public string AdminPassword { get; set; } = string.Empty;
}

public class UsuarioCreateViewModel
{
    [Required] public string Nombre { get; set; } = string.Empty;
    public string? Apellido { get; set; }
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, MinLength(8), DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    [Required, Display(Name = "Rol")] public int RolId { get; set; }
    public IEnumerable<SelectListItem> Roles { get; set; } = new List<SelectListItem>();
}

public class AreaCreateViewModel
{
    [Required, StringLength(150)] public string Nombre { get; set; } = string.Empty;
    [StringLength(500)] public string? Descripcion { get; set; }
}
