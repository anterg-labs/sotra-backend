namespace Api.DTOs.OrdenesTrabajo;

public class CambioEstadoOrdenTrabajoDto
{
    public required string Estado { get; init; }
    public string? Motivo { get; init; }
}
