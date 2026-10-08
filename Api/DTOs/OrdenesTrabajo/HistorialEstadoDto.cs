namespace Api.DTOs.OrdenesTrabajo;

public class HistorialEstadoDto
{
    public required string Estado { get; init; }
    public DateTime FechaHora { get; init; }
    public string? Observacion { get; init; }
}
