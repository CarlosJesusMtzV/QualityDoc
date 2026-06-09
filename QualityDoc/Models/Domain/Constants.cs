namespace QualityDoc.Models.Domain;

/// <summary>Nombres de rol y su nivel (menor = mas privilegios).</summary>
public static class Roles
{
    public const string SuperAdmin  = "SUPERADMIN";  // 0
    public const string Admin       = "ADMIN";       // 1
    public const string Autorizador = "AUTORIZADOR"; // 2 — autoriza (aprueba/rechaza) y además crea/edita
    public const string Revisor     = "REVISOR";     // 3 — crea/edita y manda a revisión (no autoriza)
    public const string Creador     = "CREADOR";     // 4
    public const string Lector      = "LECTOR";      // 5

    public static readonly IReadOnlyDictionary<string, int> Nivel = new Dictionary<string, int>
    {
        [SuperAdmin] = 0, [Admin] = 1, [Autorizador] = 2, [Revisor] = 3, [Creador] = 4, [Lector] = 5
    };
}

/// <summary>Estados posibles de una version de documento.</summary>
public static class EstadoVersion
{
    public const string Borrador   = "BORRADOR";
    public const string EnRevision = "EN_REVISION";
    public const string Aprobado   = "APROBADO";
    public const string Rechazado  = "RECHAZADO";
    public const string Obsoleto   = "OBSOLETO";
}

/// <summary>Tipo de cambio al rechazar (define el salto SemVer).</summary>
public static class TipoRechazo
{
    public const string Menor = "MENOR"; // patch +1
    public const string Mayor = "MAYOR"; // minor +1
}

/// <summary>Acciones registradas en auditoria (PostgreSQL).</summary>
public static class AccionAudit
{
    public const string LoginOk       = "LOGIN_OK";
    public const string LoginFallido  = "LOGIN_FALLIDO";
    public const string DocCreado     = "DOC_CREADO";
    public const string VersionSubida = "VERSION_SUBIDA";
    public const string DocEnviadoRev = "DOC_ENVIADO_REVISION";
    public const string DocAprobado   = "DOC_APROBADO";
    public const string DocRechazado  = "DOC_RECHAZADO";
    public const string UsuarioCreado = "USUARIO_CREADO";
    public const string AreaCreada    = "AREA_CREADA";
    public const string EmpresaCreada = "EMPRESA_CREADA";
}
