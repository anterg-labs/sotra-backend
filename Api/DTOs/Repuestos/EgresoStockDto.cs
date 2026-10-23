using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.Repuestos;

public class EgresoStockDto
{
    public int Cantidad { get; init; }

    [MaxLength(200)]
    public string? Motivo { get; init; }

    [MaxLength(150)]
    public string? Ubicacion { get; init; }

    public string? Observaciones { get; init; }
}
