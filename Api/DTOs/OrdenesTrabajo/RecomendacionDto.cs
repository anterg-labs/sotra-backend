namespace Api.DTOs.OrdenesTrabajo;

public class RecomendacionDto
{
    public long Id { get; init; }
    public required string Texto { get; init; }
    public required string EstadoValidacion { get; init; }
    public List<ValidacionRecomendacionDto> Validaciones { get; init; } = new();
}
