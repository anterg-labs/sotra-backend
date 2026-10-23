namespace Api.DTOs.Activos;

public class FiltrosActivosDto
{
    public string? Buscar { get; init; }
    public long? ClienteId { get; init; }
    public long? ModeloMaquinaId { get; init; }
    public string? Estado { get; init; }
    public string? OrdenarPor { get; init; }
    public string? DireccionOrden { get; init; }
    public int Pagina { get; init; } = 1;
    public int TamanoPagina { get; init; } = PaginaDto<ActivoDto>.TamanoPaginaPorDefecto;
}
