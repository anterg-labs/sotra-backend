namespace Api.DTOs.Reportes;

public class FiltrosIndicadoresDto
{
    public DateOnly? FechaDesde { get; init; }
    public DateOnly? FechaHasta { get; init; }
    public long? TecnicoId { get; init; }
    public long? ClienteId { get; init; }
    public long? ActivoId { get; init; }
    public string? Estado { get; init; }
    public string? Tipo { get; init; }
    public string? Prioridad { get; init; }
}
