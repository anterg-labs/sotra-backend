using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.Repuestos;

public class EgresoStockDto
{
    /// <summary>Cantidad (unidades, órdenes u otro conteo según el contexto).</summary>
    public int Cantidad { get; init; }

    /// <summary>Motivo del cambio o movimiento.</summary>
    [MaxLength(200)]
    public string? Motivo { get; init; }

    /// <summary>Ubicación o depósito.</summary>
    [MaxLength(150)]
    public string? Ubicacion { get; init; }

    /// <summary>Observaciones adicionales, opcionales.</summary>
    public string? Observaciones { get; init; }
}
