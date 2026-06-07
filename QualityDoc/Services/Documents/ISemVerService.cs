namespace QualityDoc.Services.Documents;

public interface ISemVerService
{
    /// <summary>Versión inicial al crear: v1.0.0 (Borrador). El número no cambia al enviar a revisión.</summary>
    (int Mayor, int Menor, int Patch) Inicial();

    /// <summary>
    /// Al aprobar: la PRIMERA aprobación del documento mantiene el número (v1.0.0 sigue v1.0.0);
    /// las siguientes (ediciones) suben de mayor (v1.0.0→v2.0.0→v3.0.0).
    /// </summary>
    (int Mayor, int Menor, int Patch) AlAprobar(int mayor, int menor, int patch, bool esPrimeraAprobacion);

    string Tag(int mayor, int menor, int patch);
}
