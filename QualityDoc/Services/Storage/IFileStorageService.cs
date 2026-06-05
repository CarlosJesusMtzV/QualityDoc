namespace QualityDoc.Services.Storage;

public record StoredFile(string RutaRelativa, string Nombre, string Tipo, string HashSha256, long TamanioBytes);

public interface IFileStorageService
{
    /// <summary>Guarda el archivo físico de una versión. Nunca sobreescribe (path único por versión).</summary>
    Task<StoredFile> GuardarAsync(string empresaSlug, int documentoId, string versionTag, IFormFile file);

    /// <summary>Abre un archivo previamente guardado. Devuelve null si no existe.</summary>
    (Stream Stream, string Nombre)? Abrir(string rutaRelativa, string nombre);
}
