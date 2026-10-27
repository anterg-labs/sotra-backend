using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.Repuestos;

public class StockMinimoDto
{
    /// <summary>Stock mínimo antes de alertar.</summary>
    [Range(0, int.MaxValue, ErrorMessage = "El stock mínimo debe ser un número entero mayor o igual a cero.")]
    [JsonRequired]
    public int CantidadMinima { get; init; }
}
