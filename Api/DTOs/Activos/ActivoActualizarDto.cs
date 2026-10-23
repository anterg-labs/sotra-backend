using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.Activos;

public class ActivoActualizarDto
{
    [Range(1, long.MaxValue)]
    [JsonRequired]
    [Required]
    public long? ClienteId { get; init; }

    [MaxLength(100, ErrorMessage = "El campo Número de serie no puede superar los 100 caracteres.")]
    [JsonRequired]
    [Required]
    public string? NumSerie { get; init; }

    [JsonRequired]
    public string? Descripcion { get; init; }

    [Range(1, long.MaxValue)]
    [JsonRequired]
    [Required]
    public long? ModeloMaquinaId { get; init; }

    [Range(1900, 2100, ErrorMessage = "El año de fabricación debe estar entre 1900 y 2100.")]
    [JsonRequired]
    public short? AnioFabricacion { get; init; }

    [MaxLength(100, ErrorMessage = "El campo Nombre interno no puede superar los 100 caracteres.")]
    [JsonRequired]
    public string? Nombre { get; init; }

    [MaxLength(150, ErrorMessage = "El campo Ubicación no puede superar los 150 caracteres.")]
    [JsonRequired]
    public string? Ubicacion { get; init; }

    [JsonRequired]
    [Required]
    public string? Estado { get; init; }
}
