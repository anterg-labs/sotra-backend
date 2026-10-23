namespace Api.DTOs.TiposMaquina;

public class FiltrosTiposMaquinaDto
{
    public string? Buscar { get; init; }
    public string? Estado { get; init; }
    public string? OrdenarPor { get; init; }
    public string? DireccionOrden { get; init; }
    public int Pagina { get; init; } = 1;
    public int TamanoPagina { get; init; } = PaginaDto<TipoMaquinaDto>.TamanoPaginaPorDefecto;
}
