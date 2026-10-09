namespace Api.DTOs.ModelosMaquina;

public class FiltrosModelosMaquinaDto
{
    public string? Buscar { get; init; }
    public long? TipoMaquinaId { get; init; }
    public string? Estado { get; init; }
    public int Pagina { get; init; } = 1;
    public int TamanoPagina { get; init; } = PaginaDto<ModeloMaquinaDto>.TamanoPaginaPorDefecto;
}
