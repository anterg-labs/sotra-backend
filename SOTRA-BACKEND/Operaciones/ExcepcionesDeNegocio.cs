namespace SOTRA_BACKEND.Operaciones;

/// <summary>
/// Un dato de la solicitud no cumple una regla de negocio (referencia inexistente, inactiva, etc.).
/// La API la traduce a <c>400</c> con su <see cref="Codigo"/>; hereda de <see cref="ArgumentException"/> para no
/// romper a quien ya la captura.
/// </summary>
public sealed class ValidacionDeNegocioException(string mensaje, string codigo = ValidacionDeNegocioException.CodigoPorDefecto)
    : ArgumentException(mensaje)
{
    public const string CodigoPorDefecto = "SOLICITUD_INVALIDA";

    public string Codigo { get; } = codigo;
}

/// <summary>
/// Una orden de trabajo no admite la operación pedida por su estado o su versión. El mensaje de la excepción es el
/// <see cref="Codigo"/> estable (hereda de <see cref="InvalidOperationException"/> por compatibilidad); la API lo
/// traduce a estado HTTP y texto en un único lugar.
/// </summary>
public sealed class ErrorDeOrdenException(string codigo) : InvalidOperationException(codigo)
{
    public const string OrdenCerrada = "ORDEN_CERRADA";
    public const string OrdenNoModificable = "ORDEN_NO_MODIFICABLE";
    public const string OrdenModificada = "ORDEN_MODIFICADA";
    public const string OrdenInexistente = "ORDEN_INEXISTENTE";
    public const string TransicionInvalida = "TRANSICION_INVALIDA";
    public const string TecnicoObligatorio = "TECNICO_OBLIGATORIO";
    public const string MotivoObligatorio = "MOTIVO_OBLIGATORIO";

    public string Codigo { get; } = codigo;
}
