using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.Repuestos;

public class StockMinimoDto
{
    [Range(0, int.MaxValue, ErrorMessage = "El stock mínimo debe ser un número entero mayor o igual a cero.")]
    public int CantidadMinima { get; init; }
}
