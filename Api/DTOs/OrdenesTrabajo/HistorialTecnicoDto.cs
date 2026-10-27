namespace Api.DTOs.OrdenesTrabajo;

/// <summary>Una asignación o un cambio de técnico: de quién a quién, quién lo hizo y cuándo.</summary>
public class HistorialTecnicoDto
{
    public long? TecnicoAnteriorId { get; init; }

    public string? NombreTecnicoAnterior { get; init; }

    public long? TecnicoId { get; init; }

    public string? NombreTecnico { get; init; }

    public DateTime FechaHora { get; init; }

    public long? UsuarioId { get; init; }

    public string? NombreUsuario { get; init; }

    public string? Observacion { get; init; }
}
