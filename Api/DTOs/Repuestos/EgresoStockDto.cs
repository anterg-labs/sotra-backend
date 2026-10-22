namespace Api.DTOs.Repuestos;

public class EgresoStockDto
{
    public int Cantidad { get; init; }
    public string? Motivo { get; init; }
    public string? Ubicacion { get; init; }
    public string? Observaciones { get; init; }
}
