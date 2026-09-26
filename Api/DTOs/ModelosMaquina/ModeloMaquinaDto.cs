namespace Api.DTOs.ModelosMaquina;

public class ModeloMaquinaDto
{
    public long Id { get; init; }
    public required string Nombre { get; init; }
    public long TipoMaquinaId { get; init; }
    public string? Descripcion { get; init; }
    public required string Estado { get; init; }
}
