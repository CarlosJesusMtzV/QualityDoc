using System.Text.Json.Serialization;

namespace QualityDoc.Services.Search;

/// <summary>
/// Cliente hacia el microservicio Node.js, que es el dueño de MongoDB.
/// .NET no escribe en Mongo: notifica a Node y consulta su API.
/// </summary>
public interface IMetadataService
{
    /// <summary>Notifica a Node para que indexe (extraiga metadatos) en segundo plano. No bloquea.</summary>
    Task NotificarIndexAsync(IndexPayload payload);

    /// <summary>Notifica un cambio de estado/vigencia/título/área (sin re-extraer).</summary>
    Task NotificarEstadoAsync(StatePayload payload);

    /// <summary>Búsqueda full-text en Node/Mongo; devuelve los Ids de versión que coinciden.</summary>
    Task<List<int>> BuscarVersionIdsAsync(int empresaId, string? q, string? area, bool soloVigentes);

    /// <summary>Búsqueda con fragmento: por cada versión, el trozo de texto donde aparece la palabra, nº de coincidencias y párrafo.</summary>
    Task<List<SearchHit>> BuscarAsync(int empresaId, string? q, string? area, bool soloVigentes);

    /// <summary>JSON crudo de los metadatos de una versión (para mostrar en vivo).</summary>
    Task<string?> ObtenerJsonAsync(int versionId);
}

/// <summary>Resultado de búsqueda con fragmento para mostrar en la lista.</summary>
public class SearchHit
{
    public int VersionId { get; set; }
    public string? Snippet { get; set; }
    public int Coincidencias { get; set; }
    public int Parrafo { get; set; }
}

public class IndexPayload
{
    [JsonPropertyName("empresa_id")]     public int EmpresaId { get; set; }
    [JsonPropertyName("empresa_slug")]   public string? EmpresaSlug { get; set; }
    [JsonPropertyName("documento_id")]   public int DocumentoId { get; set; }
    [JsonPropertyName("version_id")]     public int VersionId { get; set; }
    [JsonPropertyName("version_tag")]    public string? VersionTag { get; set; }
    [JsonPropertyName("titulo")]         public string? Titulo { get; set; }
    [JsonPropertyName("area")]           public string? Area { get; set; }
    [JsonPropertyName("tipo_archivo")]   public string? TipoArchivo { get; set; }
    [JsonPropertyName("estado")]         public string? Estado { get; set; }
    [JsonPropertyName("es_vigente")]     public bool EsVigente { get; set; }
    [JsonPropertyName("ruta_archivo")]   public string? RutaArchivo { get; set; }
    [JsonPropertyName("nombre_archivo")] public string? NombreArchivo { get; set; }
    [JsonPropertyName("nginx_url")]      public string? NginxUrl { get; set; }
    [JsonPropertyName("hash_sha256")]    public string? HashSha256 { get; set; }
    [JsonPropertyName("tamanio_bytes")]  public long? TamanioBytes { get; set; }
    [JsonPropertyName("subido_por")]     public string? SubidoPor { get; set; }

    // Opcionales: precargan texto/etiquetas/metadatos en la siembra (evitan re-extraer del archivo).
    [JsonPropertyName("texto")]          public string? Texto { get; set; }
    [JsonPropertyName("etiquetas")]      public List<string>? Etiquetas { get; set; }
    [JsonPropertyName("metadatos")]      public Dictionary<string, string>? Metadatos { get; set; }
}

public class StatePayload
{
    [JsonPropertyName("documento_id")] public int DocumentoId { get; set; }
    [JsonPropertyName("version_id")]   public int VersionId { get; set; }
    [JsonPropertyName("estado")]       public string? Estado { get; set; }
    [JsonPropertyName("es_vigente")]   public bool? EsVigente { get; set; }
    [JsonPropertyName("version_tag")]  public string? VersionTag { get; set; }
    [JsonPropertyName("titulo")]       public string? Titulo { get; set; }
    [JsonPropertyName("area")]         public string? Area { get; set; }
}
