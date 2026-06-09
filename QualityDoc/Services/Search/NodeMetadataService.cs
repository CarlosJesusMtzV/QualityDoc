using System.Net.Http.Json;
using System.Text.Json;

namespace QualityDoc.Services.Search;

/// <summary>Implementa IMetadataService llamando al microservicio Node.js por HTTP.</summary>
public class NodeMetadataService : IMetadataService
{
    private readonly HttpClient _http;
    private readonly ILogger<NodeMetadataService> _log;

    public NodeMetadataService(IHttpClientFactory factory, ILogger<NodeMetadataService> log)
    {
        _http = factory.CreateClient("Node");
        _log = log;
    }

    public async Task NotificarIndexAsync(IndexPayload payload)
    {
        try { await _http.PostAsJsonAsync("/api/index", payload); }
        catch (Exception e) { _log.LogWarning(e, "No se pudo notificar /api/index a Node (¿está arriba?)"); }
    }

    public async Task NotificarEstadoAsync(StatePayload payload)
    {
        try { await _http.PostAsJsonAsync("/api/state", payload); }
        catch (Exception e) { _log.LogWarning(e, "No se pudo notificar /api/state a Node"); }
    }

    public async Task<List<int>> BuscarVersionIdsAsync(int empresaId, string? q, string? area, bool soloVigentes)
    {
        var ids = new List<int>();
        try
        {
            var url = $"/api/search?empresaId={empresaId}&soloVigentes={(soloVigentes ? "true" : "false")}";
            if (!string.IsNullOrWhiteSpace(q)) url += $"&q={Uri.EscapeDataString(q)}";
            if (!string.IsNullOrWhiteSpace(area)) url += $"&area={Uri.EscapeDataString(area)}";

            var docs = await _http.GetFromJsonAsync<List<JsonElement>>(url);
            if (docs is not null)
                foreach (var d in docs)
                    if (d.TryGetProperty("version_id", out var v) && v.TryGetInt32(out var id))
                        ids.Add(id);
        }
        catch (Exception e) { _log.LogWarning(e, "Búsqueda en Node falló"); }
        return ids;
    }

    public async Task<List<SearchHit>> BuscarAsync(int empresaId, string? q, string? area, bool soloVigentes)
    {
        var hits = new List<SearchHit>();
        try
        {
            var url = $"/api/search?empresaId={empresaId}&soloVigentes={(soloVigentes ? "true" : "false")}";
            if (!string.IsNullOrWhiteSpace(q)) url += $"&q={Uri.EscapeDataString(q)}";
            if (!string.IsNullOrWhiteSpace(area)) url += $"&area={Uri.EscapeDataString(area)}";

            var docs = await _http.GetFromJsonAsync<List<JsonElement>>(url);
            if (docs is not null)
                foreach (var d in docs)
                {
                    if (!d.TryGetProperty("version_id", out var vid) || !vid.TryGetInt32(out var id)) continue;
                    hits.Add(new SearchHit
                    {
                        VersionId = id,
                        Snippet = d.TryGetProperty("snippet", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null,
                        Coincidencias = d.TryGetProperty("coincidencias", out var c) && c.TryGetInt32(out var cn) ? cn : 0,
                        Parrafo = d.TryGetProperty("parrafo", out var pr) && pr.TryGetInt32(out var pn) ? pn : 0
                    });
                }
        }
        catch (Exception e) { _log.LogWarning(e, "Búsqueda con fragmento en Node falló"); }
        return hits;
    }

    public async Task<string?> ObtenerJsonAsync(int versionId)
    {
        try { return await _http.GetStringAsync($"/api/metadata/{versionId}"); }
        catch (Exception e) { _log.LogWarning(e, "No se pudo obtener metadatos de Node"); return null; }
    }
}
