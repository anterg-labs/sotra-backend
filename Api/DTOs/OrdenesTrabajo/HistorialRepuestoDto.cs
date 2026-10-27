namespace Api.DTOs.OrdenesTrabajo;

/// <summary>Un cambio de un repuesto en la orden: qué se hizo, con cuántas unidades, quién y cuándo.</summary>
public class HistorialRepuestoDto
{
    /// <summary>Identificador del repuesto.</summary>
    public long RepuestoId { get; init; }
    /// <summary>Código.</summary>
    public required string Codigo { get; init; }
    /// <summary>Nombre.</summary>
    public required string Nombre { get; init; }

    /// <summary><c>reservar</c>, <c>modificar</c>, <c>entregar</c>, <c>descontar</c> o <c>liberar</c>.</summary>
    public required string Accion { get; init; }

    /// <summary>Cantidad anterior.</summary>
    public int? CantidadAnterior { get; init; }
    /// <summary>Cantidad nueva.</summary>
    public int CantidadNueva { get; init; }
    /// <summary>Estado anterior.</summary>
    public string? EstadoAnterior { get; init; }
    /// <summary>Estado al que pasó la orden.</summary>
    public required string EstadoNuevo { get; init; }
    /// <summary>Fecha y hora (ISO 8601, UTC).</summary>
    public DateTime FechaHora { get; init; }
    /// <summary>Identificador del usuario.</summary>
    public long? UsuarioId { get; init; }
    /// <summary>Nombre de la persona usuaria.</summary>
    public string? NombreUsuario { get; init; }
}
