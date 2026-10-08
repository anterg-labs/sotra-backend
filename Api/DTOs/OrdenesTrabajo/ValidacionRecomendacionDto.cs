namespace Api.DTOs.OrdenesTrabajo;

public class ValidacionRecomendacionDto
{
    public required string Resultado { get; init; }
    public string Observacion { get; init; } = "";
    public DateTime Fecha { get; init; }
}
