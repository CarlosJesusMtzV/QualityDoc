namespace QualityDoc.Models.Domain;

/// <summary>Cada subida fisica de un documento. Nunca se sobreescribe.</summary>
public class DocumentoVersion
{
    public int Id { get; set; }
    public int DocumentoId { get; set; }
    public int EmpresaId { get; set; }   // denormalizado para el filtro multi-tenant

    public int VersionMayor { get; set; }
    public int VersionMenor { get; set; }
    public int VersionPatch { get; set; }
    public string VersionTag { get; set; } = "v1.0.0";

    public string Estado { get; set; } = EstadoVersion.Borrador;
    public bool EsVigente { get; set; }      // solo una por documento
    public bool FueAprobada { get; set; }    // true si estuvo aprobada (para filtrar obsoletos)

    public string? TipoArchivo { get; set; }
    public string? NombreArchivo { get; set; }
    public string? RutaArchivo { get; set; }
    public string? HashSha256 { get; set; }
    public long? TamanioBytes { get; set; }

    // Metadatos extraídos del archivo al subirlo (se copian también a MongoDB).
    public string? TextoExtracto { get; set; }   // texto para búsqueda full-text
    public string? MetadatosJson { get; set; }   // propiedades extraídas, en JSON

    public string? ComentarioRevision { get; set; }
    public string? TipoRechazo { get; set; }  // MENOR | MAYOR

    public int SubidoPorId { get; set; }
    public int? RevisadoPorId { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime? RevisadoEn { get; set; }

    public Documento? Documento { get; set; }
    public Usuario? SubidoPor { get; set; }
    public Usuario? RevisadoPor { get; set; }
}
