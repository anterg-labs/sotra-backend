using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.Activos;

public class ActivoActualizarDto
{
    [Range(1, long.MaxValue)]
    public long? ClienteId { get; init; }

    [MaxLength(100, ErrorMessage = "El campo Número de serie no puede superar los 100 caracteres.")]
    public string? NumSerie { get; init; }

    public string? Descripcion { get; init; }

    [Range(1, long.MaxValue)]
    public long? ModeloMaquinaId { get; init; }

    [Range(1900, 2100, ErrorMessage = "El año de fabricación debe estar entre 1900 y 2100.")]
    public short? AnioFabricacion { get; init; }

    [MaxLength(100, ErrorMessage = "El campo Nombre interno no puede superar los 100 caracteres.")]
    public string? Nombre { get; init; }

    [MaxLength(150, ErrorMessage = "El campo Ubicación no puede superar los 150 caracteres.")]
    public string? Ubicacion { get; init; }

    public string? Estado { get; init; }
}
