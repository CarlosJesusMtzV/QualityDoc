namespace QualityDoc.Services.Extraction;

public record ExtraccionResultado(Dictionary<string, string> Propiedades, string TextoBusqueda);

/// <summary>Extrae metadatos de un archivo subido (PDF, Office, imágenes…).</summary>
public interface IMetadataExtractor
{
    ExtraccionResultado Extraer(Stream contenido, string nombreArchivo, string? tipo);
}
