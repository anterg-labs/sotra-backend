using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.ModelosMaquina;

public class ModeloMaquinaActualizarDto
{
    /// <summary>Nombre.</summary>
    [Required(ErrorMessage = "El campo Nombre es obligatorio."), MaxLength(60, ErrorMessage = "El campo Nombre no puede superar los 60 caracteres.")]
    public required string Nombre { get; init; }

    /// <summary>Identificador del tipo de máquina.</summary>
    [Range(1, long.MaxValue, ErrorMessage = "Debe seleccionar un tipo de máquina.")]
    public required long TipoMaquinaId { get; init; }

    /// <summary>Descripción.</summary>
    [JsonRequired]
    public string? Descripcion { get; init; }

    /// <summary>Estado del registro (`activo` o `inactivo`) o, en órdenes, valor del estado vigente del catálogo.</summary>
    [JsonRequired]
    [Required]
    public string? Estado { get; init; }
}
