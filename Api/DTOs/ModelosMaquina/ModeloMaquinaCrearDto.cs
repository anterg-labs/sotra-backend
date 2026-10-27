using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.ModelosMaquina;

public class ModeloMaquinaCrearDto
{
    /// <summary>Nombre.</summary>
    [Required(ErrorMessage = "El campo Nombre es obligatorio."), MaxLength(60, ErrorMessage = "El campo Nombre no puede superar los 60 caracteres.")]
    public required string Nombre { get; init; }

    /// <summary>Identificador del tipo de máquina.</summary>
    [Range(1, long.MaxValue, ErrorMessage = "Debe seleccionar un tipo de máquina.")]
    public long TipoMaquinaId { get; init; }

    /// <summary>Descripción.</summary>
    public string? Descripcion { get; init; }
}
