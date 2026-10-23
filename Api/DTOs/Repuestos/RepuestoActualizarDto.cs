using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Api.DTOs.Repuestos;

public class RepuestoActualizarDto
{
    [Required, MaxLength(50)]
    public required string Codigo { get; init; }

    [Required, MaxLength(120)]
    public required string Nombre { get; init; }

    [JsonRequired]
    public string? Descripcion { get; init; }

    [JsonRequired, MaxLength(100)]
    public string? Marca { get; init; }

    [JsonRequired, MaxLength(120)]
    public string? Ubicacion { get; init; }

    // Se valida por compatibilidad; el stock solo cambia mediante movimientos.
    [Range(0, int.MaxValue)]
    public int Cantidad { get; init; }

    [JsonRequired, Range(0, int.MaxValue)]
    public int CantidadMinima { get; init; }
}
