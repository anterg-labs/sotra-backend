namespace Api.DTOs.Repuestos;

public class AjusteStockDto
{
    // Stock real contado en el depósito; el movimiento registra la diferencia con el stock anterior.
    public int CantidadReal { get; init; }
    public string? Motivo { get; init; }
    public string? Observaciones { get; init; }
}
