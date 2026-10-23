namespace Api.DTOs.DocumentosTecnicos;

public class DocumentoTecnicoDto
{
    public long Id { get; init; }
    public required string NombreArchivo { get; init; }
    public required string Titulo { get; init; }
    public required string Tipo { get; init; }
    public string? Descripcion { get; init; }
    public bool Vigente { get; init; } = true;
    public long? ActivoId { get; init; }
    public string? NombreActivo { get; init; }
    public long? ModeloMaquinaId { get; init; }
    public string? NombreModelo { get; init; }
    public bool UsaSugerenciasIA { get; init; }
    public int CantidadFragmentos { get; init; }
    public DateTime CreadoEn { get; init; }
}
