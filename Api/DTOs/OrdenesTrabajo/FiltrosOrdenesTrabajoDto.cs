namespace Api.DTOs.OrdenesTrabajo;

public class FiltrosOrdenesTrabajoDto
{
    public string? Buscar { get; init; }
    public long? ClienteId { get; init; }
    public long? ActivoId { get; init; }
    public long? TecnicoId { get; init; }
    public string? Estado { get; init; }
    public string? Prioridad { get; init; }
    public string? Tipo { get; init; }
    public DateOnly? FechaDesde { get; init; }
    public DateOnly? FechaHasta { get; init; }
    public bool? ConSugerencias { get; init; }
    public string? OrdenarPor { get; init; }
    public string? DireccionOrden { get; init; }
    public int Pagina { get; init; } = 1;
    public int TamanoPagina { get; init; } = PaginaDto<OrdenTrabajoDto>.TamanoPaginaPorDefecto;
}
