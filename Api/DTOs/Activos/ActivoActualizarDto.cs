using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.Activos;

public class ActivoActualizarDto
{
    /// <summary>Identificador del cliente.</summary>
    [Range(1, long.MaxValue)]
    [JsonRequired]
    [Required]
    public long? ClienteId { get; init; }

    /// <summary>Número de serie.</summary>
    [MaxLength(100, ErrorMessage = "El campo Número de serie no puede superar los 100 caracteres.")]
    [JsonRequired]
    [Required]
    public string? NumSerie { get; init; }

    /// <summary>Descripción.</summary>
    [JsonRequired]
    public string? Descripcion { get; init; }

    /// <summary>Identificador del modelo de máquina.</summary>
    [Range(1, long.MaxValue)]
    [JsonRequired]
    [Required]
    public long? ModeloMaquinaId { get; init; }

    /// <summary>Año de fabricación.</summary>
    [Range(1900, 2100, ErrorMessage = "El año de fabricación debe estar entre 1900 y 2100.")]
    [JsonRequired]
    public short? AnioFabricacion { get; init; }

    /// <summary>Nombre.</summary>
    [MaxLength(100, ErrorMessage = "El campo Nombre interno no puede superar los 100 caracteres.")]
    [JsonRequired]
    public string? Nombre { get; init; }

    /// <summary>Ubicación o depósito.</summary>
    [MaxLength(150, ErrorMessage = "El campo Ubicación no puede superar los 150 caracteres.")]
    [JsonRequired]
    public string? Ubicacion { get; init; }

    /// <summary>Estado del registro (`activo` o `inactivo`) o, en órdenes, valor del estado vigente del catálogo.</summary>
    [JsonRequired]
    [Required]
    public string? Estado { get; init; }

    /// <summary>Cómo se mide el uso del activo: `horas` (horómetro) o `kilometros` (odómetro). Si no se envía, no cambia lo guardado.</summary>
    [RegularExpression("^(horas|kilometros)$", ErrorMessage = "La unidad de uso debe ser 'horas' o 'kilometros'.")]
    public string? UnidadUso { get; init; }
}
