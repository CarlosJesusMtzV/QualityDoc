using MongoDB.Driver;
using QualityDoc.Models.Mongo;

namespace QualityDoc.Services.Search;

public class MetadataService : IMetadataService
{
    private readonly IMongoCollection<DocumentMetadata> _col;

    public MetadataService(IMongoDatabase db) =>
        _col = db.GetCollection<DocumentMetadata>("document_metadata");

    public async Task UpsertAsync(DocumentMetadata meta)
    {
        var filter = Builders<DocumentMetadata>.Filter.And(
            Builders<DocumentMetadata>.Filter.Eq(x => x.DocumentoId, meta.DocumentoId),
            Builders<DocumentMetadata>.Filter.Eq(x => x.VersionId, meta.VersionId));
        await _col.ReplaceOneAsync(filter, meta, new ReplaceOptions { IsUpsert = true });
    }

    public async Task<List<DocumentMetadata>> BuscarAsync(int empresaId, string? texto, string? area, bool soloVigentes)
    {
        var fb = Builders<DocumentMetadata>.Filter;
        var filter = fb.Eq(x => x.EmpresaId, empresaId);
        if (!string.IsNullOrWhiteSpace(area)) filter &= fb.Eq(x => x.Area, area);
        if (soloVigentes) filter &= fb.Eq(x => x.EsVigente, true);
        if (!string.IsNullOrWhiteSpace(texto)) filter &= fb.Text(texto);

        return await _col.Find(filter).Limit(200).ToListAsync();
    }

    public async Task<DocumentMetadata?> ObtenerAsync(int versionId) =>
        await _col.Find(Builders<DocumentMetadata>.Filter.Eq(x => x.VersionId, versionId)).FirstOrDefaultAsync();
}
