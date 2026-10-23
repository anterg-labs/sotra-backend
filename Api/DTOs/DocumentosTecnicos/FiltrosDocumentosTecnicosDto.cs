namespace Api.DTOs.DocumentosTecnicos;

public class FiltrosDocumentosTecnicosDto
{
    public string? Buscar { get; init; }
    public long? ActivoId { get; init; }
    public long? ModeloMaquinaId { get; init; }
    public string? Tipo { get; init; }
    public bool? Vigente { get; init; }
    public string? OrdenarPor { get; init; }
    public string? DireccionOrden { get; init; }
    public int Pagina { get; init; } = 1;
    public int TamanoPagina { get; init; } = PaginaDto<DocumentoTecnicoDto>.TamanoPaginaPorDefecto;
}
