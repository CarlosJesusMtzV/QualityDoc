namespace QualityDoc.Services.Documents;

public interface ISemVerService
{
    /// <summary>Versión inicial al crear un documento: v0.1.0 (borrador, sin aprobar).</summary>
    (int Mayor, int Menor, int Patch) Inicial();

    /// <summary>Al rechazar: cambio menor = patch+1; cambio mayor = minor+1.</summary>
    (int Mayor, int Menor, int Patch) Rechazo(int mayor, int menor, int patch, string tipoRechazo);

    /// <summary>Al aprobar: si es pre-release (mayor=0) promueve a v1.0.0; si ya es entero, mantiene el número.</summary>
    (int Mayor, int Menor, int Patch) AlAprobar(int mayor, int menor, int patch);

    /// <summary>Nueva edición (editar = reemplazar archivo): siguiente mayor (.0.0).</summary>
    (int Mayor, int Menor, int Patch) NuevaEdicion(int mayorActualMaximo);

    string Tag(int mayor, int menor, int patch);
}
