namespace QualityDoc.Services.Storage;

public record StoredFile(string RutaRelativa, string Nombre, string Tipo, string HashSha256, long TamanioBytes);

public interface IFileStorageService
{
    /// <summary>
    /// Guarda el archivo físico de una versión, renombrándolo a <paramref name="nombreBase"/>
    /// (ej. "Título - Área") conservando la extensión original. Path único por versión.
    /// </summary>
    Task<StoredFile> GuardarAsync(string empresaSlug, int documentoId, string versionTag, IFormFile file, string nombreBase);

    /// <summary>Abre un archivo previamente guardado. Devuelve null si no existe.</summary>
    (Stream Stream, string Nombre)? Abrir(string rutaRelativa, string nombre);

    /// <summary>Guarda bytes directamente (usado por la siembra de datos demo).</summary>
    StoredFile GuardarBytes(string empresaSlug, int documentoId, string versionTag, string nombreArchivo, byte[] contenido);
}
