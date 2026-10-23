namespace Api.DTOs.Repuestos;

public class FiltrosRepuestosDto
{
    public string? Buscar { get; init; }
    public string? Estado { get; init; }
    public string? Ubicacion { get; init; }
    public bool? BajoStock { get; init; }
    public string? EstadoStock { get; init; }
    public string? Orden { get; init; }
    public string? OrdenarPor { get; init; }
    public string? DireccionOrden { get; init; }
    public int Pagina { get; init; } = 1;
    public int TamanoPagina { get; init; } = PaginaDto<RepuestoDto>.TamanoPaginaPorDefecto;
}
