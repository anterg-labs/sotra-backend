using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Api.DTOs.OrdenesTrabajo;

/// <summary>Los repuestos y cantidades que debe quedar usando la orden (reemplaza a los actuales).</summary>
public class OrdenRepuestosActualizarDto
{
    /// <summary>Repuestos con sus cantidades.</summary>
    [JsonRequired, Required]
    public List<RepuestoPedidoDto> Repuestos { get; init; } = [];
}
