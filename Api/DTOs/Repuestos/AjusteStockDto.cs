using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.Repuestos;

public class AjusteStockDto
{
    /// <summary>Stock real contado; el sistema calcula la diferencia con el stock registrado.</summary>
    public int CantidadReal { get; init; }

    /// <summary>Motivo del cambio o movimiento.</summary>
    [MaxLength(200)]
    public string? Motivo { get; init; }

    /// <summary>Observaciones adicionales, opcionales.</summary>
    public string? Observaciones { get; init; }
}
