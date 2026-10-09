namespace Api.DTOs.DocumentosTecnicos;

public class FiltrosDocumentosTecnicosDto
{
    public string? Buscar { get; init; }
    public long? ActivoId { get; init; }
    public int Pagina { get; init; } = 1;
    public int TamanoPagina { get; init; } = PaginaDto<DocumentoTecnicoDto>.TamanoPaginaPorDefecto;
}
