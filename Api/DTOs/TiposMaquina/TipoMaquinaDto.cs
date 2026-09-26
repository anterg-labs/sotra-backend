namespace Api.DTOs.TiposMaquina;

public class TipoMaquinaDto
{
    public long Id { get; init; }
    public required string Nombre { get; init; }
    public required string Estado { get; init; }
}
