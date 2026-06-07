using QualityDoc.Models.Domain;

namespace QualityDoc.Services.Documents;

/// <summary>
/// Reglas de versionado (el número solo cambia al APROBAR o RECHAZAR, nunca al crear/editar/enviar):
///   Crear                      → v1.0.0 (Borrador)
///   Enviar a revisión          → sin cambio
///   Editar (nueva versión)     → borrador que HEREDA el número del vigente (sin cambio aún)
///   Aprobar (1ª vez)           → mantiene el número (v1.0.0 → v1.0.0, Vigente)
///   Aprobar (edición)          → +mayor (v1.0.0 → v2.0.0 → v3.0.0); la anterior pasa a Obsoleta
///   Rechazar                   → 2º número +1 (v2.0.0 → v2.1.0); queda RECHAZADO, sin crear borrador.
/// </summary>
public class SemVerService : ISemVerService
{
    public (int Mayor, int Menor, int Patch) Inicial() => (1, 0, 0);

    public (int Mayor, int Menor, int Patch) AlAprobar(int mayor, int menor, int patch, bool esPrimeraAprobacion) =>
        esPrimeraAprobacion ? (mayor, menor, patch) : (mayor + 1, 0, 0);

    public string Tag(int mayor, int menor, int patch) => $"v{mayor}.{menor}.{patch}";
}
