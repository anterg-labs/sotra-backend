namespace Api.DTOs.MovimientosStock;

public class FiltrosMovimientosStockDto
{
    public string? Buscar { get; init; }
    public long? RepuestoId { get; init; }
    public string? Tipo { get; init; }
    public DateOnly? FechaDesde { get; init; }
    public DateOnly? FechaHasta { get; init; }
    public string? Ubicacion { get; init; }
    public long? UsuarioId { get; init; }
    public int Pagina { get; init; } = 1;
    public int TamanoPagina { get; init; } = PaginaDto<MovimientoStockDto>.TamanoPaginaPorDefecto;
}
