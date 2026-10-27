namespace Api.DTOs.OrdenesTrabajo;

/// <summary>Un cambio de estado de la orden: de qué estado vino, a cuál pasó, cuándo, quién lo hizo y el motivo.</summary>
public class HistorialEstadoDto
{
    public required string Estado { get; init; }

    /// <summary>Estado del que se vino; <c>null</c> en el alta de la orden.</summary>
    public string? EstadoAnterior { get; init; }

    public DateTime FechaHora { get; init; }

    /// <summary>Motivo u observación del cambio.</summary>
    public string? Observacion { get; init; }

    /// <summary>Quién hizo el cambio; <c>null</c> si no se pudo identificar (cambios anteriores al registro).</summary>
    public long? UsuarioId { get; init; }

    public string? NombreUsuario { get; init; }
}
