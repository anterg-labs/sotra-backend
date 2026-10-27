using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.CausasFalla;

public class CausaFallaCrearDto
{
    /// <summary>Nombre de la causa, único sin distinguir mayúsculas.</summary>
    [Required(ErrorMessage = "El campo Nombre es obligatorio."), MaxLength(80, ErrorMessage = "El campo Nombre no puede superar los 80 caracteres.")]
    public required string Nombre { get; init; }
}
