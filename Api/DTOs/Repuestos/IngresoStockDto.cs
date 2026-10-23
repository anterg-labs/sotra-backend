namespace Api.DTOs.Repuestos;

/// <summary>
/// Un ingreso de stock solo pide la cantidad (y, si se quiere, una observación). La fecha la pone el sistema, la ubicación
/// es la del repuesto (se define al registrarlo) y el usuario se toma de la sesión cuando exista.
/// </summary>
public class IngresoStockDto
{
    public int Cantidad { get; init; }
    public string? Observaciones { get; init; }
}
