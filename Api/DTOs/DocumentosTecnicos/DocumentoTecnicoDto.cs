namespace Api.DTOs.DocumentosTecnicos;

public class DocumentoTecnicoDto
{
    public long Id { get; init; }
    public required string NombreArchivo { get; init; }
    public long? ActivoId { get; init; }
    public string? NombreActivo { get; init; }
    public bool UsaSugerenciasIA { get; init; }
    public int CantidadFragmentos { get; init; }
    public DateTime CreadoEn { get; init; }
}
