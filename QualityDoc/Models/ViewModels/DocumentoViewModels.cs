using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QualityDoc.Models.ViewModels;

public class DocumentoCreateViewModel
{
    [Required(ErrorMessage = "El título es obligatorio.")]
    [StringLength(300)]
    public string Titulo { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Descripcion { get; set; }

    [Required(ErrorMessage = "Selecciona un área.")]
    [Display(Name = "Área")]
    public int AreaId { get; set; }

    [Required(ErrorMessage = "Sube un archivo.")]
    public IFormFile? Archivo { get; set; }

    public IEnumerable<SelectListItem> Areas { get; set; } = new List<SelectListItem>();
}

public class DocumentoEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El título es obligatorio.")]
    [StringLength(300)]
    public string Titulo { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Descripcion { get; set; }
}
