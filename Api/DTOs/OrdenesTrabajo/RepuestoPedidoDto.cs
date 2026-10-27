using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Api.DTOs.OrdenesTrabajo;

/// <summary>Un repuesto y la cantidad que usa la orden.</summary>
public class RepuestoPedidoDto
{
    [JsonRequired, Range(1, long.MaxValue)]
    public long RepuestoId { get; init; }

    [JsonRequired, Range(1, 1_000_000)]
    public int Cantidad { get; init; }
}
