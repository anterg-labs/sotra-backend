namespace Api.DTOs.Repuestos;

public class AlertasStockDto
{
    /// <summary>Repuestos con stock bajo.</summary>
    public int Bajo { get; init; }
    /// <summary>Repuestos sin stock.</summary>
    public int SinStock { get; init; }
    /// <summary>Total.</summary>
    public int Total { get; init; }
}
