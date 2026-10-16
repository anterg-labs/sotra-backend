using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.ModelosMaquina;

public class ModeloMaquinaActualizarDto
{
    [Required(ErrorMessage = "El campo Nombre es obligatorio."), MaxLength(60, ErrorMessage = "El campo Nombre no puede superar los 60 caracteres.")]
    public required string Nombre { get; init; }

    [Range(1, long.MaxValue, ErrorMessage = "Debe seleccionar un tipo de máquina.")]
    public required long TipoMaquinaId { get; init; }

    public string? Descripcion { get; init; }

    public string? Estado { get; init; }
}
