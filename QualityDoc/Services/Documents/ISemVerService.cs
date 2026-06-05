namespace QualityDoc.Services.Documents;

public interface ISemVerService
{
    /// <summary>Calcula el siguiente número de versión según la acción.</summary>
    (int Mayor, int Menor, int Patch) Siguiente(
        int mayor, int menor, int patch, string? tipoRechazo, bool esAprobacion);

    string Tag(int mayor, int menor, int patch);
}
