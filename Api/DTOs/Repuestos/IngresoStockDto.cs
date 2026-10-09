namespace Api.DTOs.Repuestos;

public class IngresoStockDto
{
    public int Cantidad { get; init; }
    public DateOnly Fecha { get; init; }
    public string? Motivo { get; init; }
    public string? Ubicacion { get; init; }
    public string? Observaciones { get; init; }
}
