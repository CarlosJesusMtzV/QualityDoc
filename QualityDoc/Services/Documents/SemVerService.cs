using QualityDoc.Models.Domain;

namespace QualityDoc.Services.Documents;

/// <summary>
/// Reglas SemVer:
///   Creación              → v1.0.0
///   Rechazo cambio MENOR  → patch +1     (v1.0.0 → v1.0.1)
///   Rechazo cambio MAYOR  → minor +1     (v1.0.0 → v1.1.0, patch a 0)
///   Aprobación            → major +1     (v1.3.2 → v2.0.0, minor y patch a 0)
/// </summary>
public class SemVerService : ISemVerService
{
    public (int Mayor, int Menor, int Patch) Siguiente(
        int mayor, int menor, int patch, string? tipoRechazo, bool esAprobacion)
    {
        if (esAprobacion)
            return (mayor + 1, 0, 0);

        return tipoRechazo switch
        {
            TipoRechazo.Menor => (mayor, menor, patch + 1),
            TipoRechazo.Mayor => (mayor, menor + 1, 0),
            _ => throw new ArgumentException($"TipoRechazo inválido: '{tipoRechazo}'. Use MENOR o MAYOR.")
        };
    }

    public string Tag(int mayor, int menor, int patch) => $"v{mayor}.{menor}.{patch}";
}
