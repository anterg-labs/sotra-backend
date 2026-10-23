using Microsoft.EntityFrameworkCore;
using Npgsql;
using SOTRA_BACKEND.Operaciones;

namespace Api.Infraestructura;

/// <summary>
/// Un único lugar donde se decide qué estado HTTP, código y texto recibe quien pide algo que choca con una restricción
/// única o con el estado de una orden. Antes cada controlador repetía estos bloques <c>catch</c>.
/// </summary>
public static class TraduccionDeErroresDeNegocio
{
    public readonly record struct Respuesta(int Estado, string Codigo, string Mensaje);

    // Restricciones únicas de PostgreSQL con su propio código y texto.
    private static readonly Dictionary<string, (string Codigo, string Mensaje)> PorRestriccion = new()
    {
        ["UK_RAZON_SOCIAL_CLIENTE"] = ("RAZON_SOCIAL_DUPLICADA", "El nombre o razón social ya está registrado."),
        ["UK_NOMBRE_CLIENTE"] = ("NOMBRE_CONTACTO_DUPLICADO", "El nombre de contacto ya está registrado."),
        ["UK_CORREO_CLIENTE"] = ("CORREO_DUPLICADO", "El correo electrónico ya está registrado."),
        ["UK_TELEFONO_CLIENTE"] = ("TELEFONO_DUPLICADO", "El número telefónico ya está registrado."),
        ["UK_MAIL_USUARIO"] = ("CORREO_DUPLICADO", "El correo electrónico ya está registrado."),
        ["UK_TELEFONO_USUARIO"] = ("TELEFONO_DUPLICADO", "El número de teléfono ya está registrado."),
    };

    // Si la restricción no tiene texto propio, se usa el de la tabla.
    private static readonly Dictionary<string, (string Codigo, string Mensaje)> PorTabla = new()
    {
        ["cliente"] = ("CLIENTE_DUPLICADO", "Ya existe un cliente con la razón social, nombre, correo o teléfono indicado."),
        ["usuario"] = ("USUARIO_DUPLICADO", "Ya existe un usuario con el correo o teléfono indicado."),
        ["activo"] = ("NUMERO_SERIE_DUPLICADO", "Ya existe un activo con ese número de serie."),
        ["tipo_maquina"] = ("TIPO_MAQUINA_DUPLICADO", "Ya existe un tipo de máquina con ese nombre."),
        ["modelo_maquina"] = ("MODELO_DUPLICADO", "Ya existe un modelo de máquina con ese nombre para el tipo seleccionado."),
        ["repuesto"] = ("CODIGO_REPUESTO_DUPLICADO", "Ya existe un repuesto con ese código."),
    };

    private static readonly Dictionary<string, Respuesta> PorEstadoDeOrden = new()
    {
        [ErrorDeOrdenException.OrdenCerrada] = new(StatusCodes.Status409Conflict, ErrorDeOrdenException.OrdenCerrada,
            "La orden cerrada no se puede modificar."),
        [ErrorDeOrdenException.OrdenNoModificable] = new(StatusCodes.Status409Conflict, ErrorDeOrdenException.OrdenNoModificable,
            "La orden cerrada o cancelada no se puede modificar."),
        [ErrorDeOrdenException.OrdenModificada] = new(StatusCodes.Status412PreconditionFailed, ErrorDeOrdenException.OrdenModificada,
            "Otra persona modificó la orden mientras la editabas. Recargala para ver los cambios."),
        [ErrorDeOrdenException.OrdenInexistente] = new(StatusCodes.Status404NotFound, "RECURSO_NO_ENCONTRADO",
            "La orden de trabajo no existe."),
        [ErrorDeOrdenException.TransicionInvalida] = new(StatusCodes.Status400BadRequest, ErrorDeOrdenException.TransicionInvalida,
            "La orden no puede pasar a ese estado desde su estado actual."),
        [ErrorDeOrdenException.TecnicoObligatorio] = new(StatusCodes.Status400BadRequest, ErrorDeOrdenException.TecnicoObligatorio,
            "Asigná un técnico antes de pasar la orden a ese estado."),
        [ErrorDeOrdenException.TecnicoYaAsignado] = new(StatusCodes.Status409Conflict, ErrorDeOrdenException.TecnicoYaAsignado,
            "El técnico ya está asignado a esta orden de trabajo."),
        [ErrorDeOrdenException.MotivoObligatorio] = new(StatusCodes.Status400BadRequest, ErrorDeOrdenException.MotivoObligatorio,
            "Indicá el motivo u observación del cambio de estado."),
    };

    /// <summary>Devuelve la respuesta que corresponde a una excepción de negocio conocida, o <c>null</c> si no lo es.</summary>
    public static Respuesta? Traducir(Exception excepcion) => excepcion switch
    {
        ConflictoDeDatosException conflicto => new(StatusCodes.Status409Conflict, conflicto.Codigo, conflicto.Message),
        ValidacionDeNegocioException validacion => new(StatusCodes.Status400BadRequest, validacion.Codigo, validacion.Message),
        ErrorDeOrdenException orden when PorEstadoDeOrden.TryGetValue(orden.Codigo, out var respuesta) => respuesta,
        DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg } =>
            TraducirDuplicado(pg),
        _ => null,
    };

    private static Respuesta TraducirDuplicado(PostgresException excepcion)
    {
        if (excepcion.ConstraintName is { } restriccion && PorRestriccion.TryGetValue(restriccion, out var propia))
        {
            return new(StatusCodes.Status409Conflict, propia.Codigo, propia.Mensaje);
        }

        if (excepcion.TableName is { } tabla && PorTabla.TryGetValue(tabla, out var deLaTabla))
        {
            return new(StatusCodes.Status409Conflict, deLaTabla.Codigo, deLaTabla.Mensaje);
        }

        return new(StatusCodes.Status409Conflict, "DATO_DUPLICADO", "Ya existe un registro con esos datos.");
    }
}
