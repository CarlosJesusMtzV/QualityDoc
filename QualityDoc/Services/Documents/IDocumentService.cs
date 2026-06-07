using QualityDoc.Models.Domain;

namespace QualityDoc.Services.Documents;

public interface IDocumentService
{
    /// <summary>Crea un documento con su versión inicial v1.0.0 (BORRADOR).</summary>
    Task<int> CrearAsync(string titulo, string? descripcion, int areaId, IFormFile archivo);

    /// <summary>Edita SOLO los datos del documento (título y descripción). El área no cambia.</summary>
    Task EditarAsync(int documentoId, string titulo, string? descripcion);

    /// <summary>Baja lógica del documento (deja de aparecer en listados).</summary>
    Task EliminarAsync(int documentoId);

    /// <summary>Nueva edición: sube un archivo de reemplazo como nueva versión (siguiente mayor) en BORRADOR.</summary>
    Task<int> NuevaVersionAsync(int documentoId, IFormFile archivo);

    /// <summary>Reemplaza el archivo de una versión en BORRADOR (editar antes de enviar).</summary>
    Task SubirArchivoAsync(int versionId, IFormFile archivo);

    Task EnviarARevisionAsync(int versionId);
    Task AprobarAsync(int versionId, string? comentario);
    Task RechazarAsync(int versionId, string? comentario);

    /// <summary>Listado paginado. vista = "vigentes" (solo vigentes) | "proceso" (borradores y en revisión).</summary>
    Task<(List<DocumentoVersion> Items, int Total)> ListarAsync(int? areaId, string? tipo, string vista, int page, int pageSize);

    /// <summary>Trae versiones por sus Ids (usado por la búsqueda full-text de MongoDB).</summary>
    Task<List<DocumentoVersion>> ListarPorIdsAsync(IEnumerable<int> versionIds, bool soloVigentes);

    /// <summary>Documento con todo su historial de versiones.</summary>
    Task<Documento?> ObtenerDetalleAsync(int documentoId);

    Task<DocumentoVersion?> ObtenerVersionAsync(int versionId);
}
