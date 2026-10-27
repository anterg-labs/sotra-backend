using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Api.DTOs.OrdenesTrabajo;

/// <summary>Un repuesto y la cantidad que usa la orden.</summary>
public class RepuestoPedidoDto
{
    /// <summary>Identificador del repuesto.</summary>
    [JsonRequired, Range(1, long.MaxValue)]
    public long RepuestoId { get; init; }

    /// <summary>Cantidad (unidades, órdenes u otro conteo según el contexto).</summary>
    [JsonRequired, Range(1, 1_000_000)]
    public int Cantidad { get; init; }
}
