using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace QualityDoc.Models.Mongo;

/// <summary>Metadatos de una version de documento, para busqueda en MongoDB.</summary>
public class DocumentMetadata
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("empresa_id")]     public int EmpresaId { get; set; }
    [BsonElement("empresa_slug")]   public string EmpresaSlug { get; set; } = string.Empty;
    [BsonElement("documento_id")]   public int DocumentoId { get; set; }
    [BsonElement("version_id")]     public int VersionId { get; set; }
    [BsonElement("version_tag")]    public string VersionTag { get; set; } = string.Empty;
    [BsonElement("titulo")]         public string Titulo { get; set; } = string.Empty;
    [BsonElement("area")]           public string Area { get; set; } = string.Empty;
    [BsonElement("tipo_archivo")]   public string? TipoArchivo { get; set; }
    [BsonElement("etiquetas")]      public List<string> Etiquetas { get; set; } = new();
    [BsonElement("estado")]         public string Estado { get; set; } = string.Empty;
    [BsonElement("es_vigente")]     public bool EsVigente { get; set; }
    [BsonElement("ruta_archivo")]   public string? RutaArchivo { get; set; }
    [BsonElement("nginx_url")]      public string? NginxUrl { get; set; }   // URL pública servida por Nginx
    [BsonElement("hash_sha256")]    public string? HashSha256 { get; set; }
    [BsonElement("tamanio_bytes")]  public long? TamanioBytes { get; set; }
    [BsonElement("subido_por")]     public string? SubidoPor { get; set; }
    [BsonElement("texto_extracto")] public string? TextoExtracto { get; set; }
    // Metadatos extraídos del archivo, guardados como objeto JSON anidado.
    [BsonElement("metadatos")]      public Dictionary<string, string> Metadatos { get; set; } = new();
    [BsonElement("fecha_subida")]   public DateTime FechaSubida { get; set; } = DateTime.UtcNow;
}
