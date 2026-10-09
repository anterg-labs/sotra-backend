namespace Api.DTOs.Usuarios;

public class FiltrosUsuariosDto
{
    public string? Buscar { get; init; }
    public long? RolId { get; init; }
    public string? Estado { get; init; }
    public string? DireccionOrden { get; init; }
    public int Pagina { get; init; } = 1;
    public int TamanoPagina { get; init; } = PaginaDto<UsuarioListadoDto>.TamanoPaginaPorDefecto;
}
