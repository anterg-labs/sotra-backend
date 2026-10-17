using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.TiposMaquina;

public class TipoMaquinaActualizarDto
{
    [Required(ErrorMessage = "El campo Nombre es obligatorio."), MaxLength(60, ErrorMessage = "El campo Nombre no puede superar los 60 caracteres.")]
    public required string Nombre { get; init; }

    public string? Estado { get; init; }
}
