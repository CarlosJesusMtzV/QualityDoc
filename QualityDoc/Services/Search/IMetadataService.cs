using QualityDoc.Models.Mongo;

namespace QualityDoc.Services.Search;

/// <summary>Sincroniza/busca metadatos de versiones en MongoDB.</summary>
public interface IMetadataService
{
    Task UpsertAsync(DocumentMetadata meta);
    Task<List<DocumentMetadata>> BuscarAsync(int empresaId, string? texto, string? area, bool soloVigentes);
    Task<DocumentMetadata?> ObtenerAsync(int versionId);
}
