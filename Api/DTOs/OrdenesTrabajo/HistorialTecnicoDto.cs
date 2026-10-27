namespace Api.DTOs.OrdenesTrabajo;

/// <summary>Una asignación o un cambio de técnico: de quién a quién, quién lo hizo y cuándo.</summary>
public class HistorialTecnicoDto
{
    /// <summary>Identificador del técnico anterior.</summary>
    public long? TecnicoAnteriorId { get; init; }

    /// <summary>Nombre del técnico anterior.</summary>
    public string? NombreTecnicoAnterior { get; init; }

    /// <summary>Identificador del técnico.</summary>
    public long? TecnicoId { get; init; }

    /// <summary>Nombre del técnico.</summary>
    public string? NombreTecnico { get; init; }

    /// <summary>Fecha y hora (ISO 8601, UTC).</summary>
    public DateTime FechaHora { get; init; }

    /// <summary>Identificador del usuario.</summary>
    public long? UsuarioId { get; init; }

    /// <summary>Nombre de la persona usuaria.</summary>
    public string? NombreUsuario { get; init; }

    /// <summary>Observación.</summary>
    public string? Observacion { get; init; }
}
