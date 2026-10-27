using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.Activos;

public class ActivoCrearDto
{
    /// <summary>Identificador del cliente.</summary>
    [Range(1, long.MaxValue, ErrorMessage = "Debe seleccionar un cliente.")]
    public long ClienteId { get; init; }

    /// <summary>Número de serie.</summary>
    [Required(ErrorMessage = "El campo Número de serie es obligatorio."), MaxLength(100, ErrorMessage = "El campo Número de serie no puede superar los 100 caracteres.")]
    public required string NumSerie { get; init; }

    /// <summary>Descripción.</summary>
    public string? Descripcion { get; init; }

    /// <summary>Identificador del modelo de máquina.</summary>
    [Range(1, long.MaxValue, ErrorMessage = "Debe seleccionar un modelo de máquina.")]
    public long ModeloMaquinaId { get; init; }

    /// <summary>Año de fabricación.</summary>
    [Range(1900, 2100, ErrorMessage = "El año de fabricación debe estar entre 1900 y 2100.")]
    public short? AnioFabricacion { get; init; }

    /// <summary>Nombre.</summary>
    [MaxLength(100, ErrorMessage = "El campo Nombre interno no puede superar los 100 caracteres.")]
    public string? Nombre { get; init; }

    /// <summary>Ubicación o depósito.</summary>
    [MaxLength(150, ErrorMessage = "El campo Ubicación no puede superar los 150 caracteres.")]
    public string? Ubicacion { get; init; }
}
