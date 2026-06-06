using QualityDoc.Models.Domain;

namespace QualityDoc.Services.Documents;

/// <summary>
/// Reglas de versionado:
///   Crear                 → v0.1.0 (borrador, sin aprobar)
///   Rechazo cambio MENOR  → patch +1     (v0.1.0 → v0.1.1 | v2.0.0 → v2.0.1)
///   Rechazo cambio MAYOR  → minor +1     (v0.1.0 → v0.2.0 | v2.0.0 → v2.1.0)
///   Aprobar (1ª vez, 0.x) → v1.0.0       (promueve de pre-release a entero)
///   Aprobar (edición)     → mantiene el número (solo marca Vigente)
///   Nueva edición (editar)→ siguiente mayor .0.0 (v1.0.0 → v2.0.0)
/// </summary>
public class SemVerService : ISemVerService
{
    public (int Mayor, int Menor, int Patch) Inicial() => (0, 1, 0);

    public (int Mayor, int Menor, int Patch) Rechazo(int mayor, int menor, int patch, string tipoRechazo) =>
        tipoRechazo switch
        {
            TipoRechazo.Menor => (mayor, menor, patch + 1),
            TipoRechazo.Mayor => (mayor, menor + 1, 0),
            _ => throw new ArgumentException($"TipoRechazo inválido: '{tipoRechazo}'. Use MENOR o MAYOR.")
        };

    public (int Mayor, int Menor, int Patch) AlAprobar(int mayor, int menor, int patch) =>
        mayor == 0 ? (1, 0, 0) : (mayor, menor, patch);

    public (int Mayor, int Menor, int Patch) NuevaEdicion(int mayorActualMaximo) =>
        (mayorActualMaximo + 1, 0, 0);

    public string Tag(int mayor, int menor, int patch) => $"v{mayor}.{menor}.{patch}";
}
