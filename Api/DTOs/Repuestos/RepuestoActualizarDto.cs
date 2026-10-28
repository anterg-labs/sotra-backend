using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Api.DTOs.Repuestos;

public class RepuestoActualizarDto
{
    /// <summary>Código.</summary>
    [Required, MaxLength(50)]
    public required string Codigo { get; init; }

    /// <summary>Nombre.</summary>
    [Required, MaxLength(120)]
    public required string Nombre { get; init; }

    /// <summary>Descripción.</summary>
    [JsonRequired]
    public string? Descripcion { get; init; }

    /// <summary>Marca.</summary>
    [JsonRequired, MaxLength(100)]
    public string? Marca { get; init; }

    /// <summary>Ubicación o depósito.</summary>
    [JsonRequired, MaxLength(120)]
    public string? Ubicacion { get; init; }

    // Se valida por compatibilidad; el stock solo cambia mediante movimientos.
    /// <summary>Cantidad (unidades, órdenes u otro conteo según el contexto).</summary>
    [Range(0, int.MaxValue)]
    public int Cantidad { get; init; }

    /// <summary>Stock mínimo antes de alertar.</summary>
    [JsonRequired, Range(0, int.MaxValue)]
    public int CantidadMinima { get; init; }

    /// <summary>Costo unitario del repuesto, opcional. Si no se envía, no cambia lo guardado.</summary>
    [Range(0.0, 999999999.0, ErrorMessage = "El costo unitario no puede ser negativo.")]
    public decimal? CostoUnitario { get; init; }
}
