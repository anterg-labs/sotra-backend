using System.ComponentModel.DataAnnotations;
using Api.Validaciones;

namespace Api.DTOs.Usuarios;

public class UsuarioActualizarDto
{
    [Required(ErrorMessage = "El campo Nombre es obligatorio."), MaxLength(100, ErrorMessage = "El campo Nombre no puede superar los 100 caracteres."), NombrePersona]
    public required string Nombre { get; init; }

    [Required(ErrorMessage = "El campo Apellido es obligatorio."), MaxLength(100, ErrorMessage = "El campo Apellido no puede superar los 100 caracteres."), NombrePersona]
    public required string Apellido { get; init; }

    [Required(ErrorMessage = "El campo Correo es obligatorio."), EmailAddress(ErrorMessage = "El correo no tiene un formato válido."), MaxLength(254, ErrorMessage = "El correo no puede superar los 254 caracteres.")]
    public required string Correo { get; init; }

    [Required(ErrorMessage = "El campo teléfono es obligatorio."), MaxLength(30, ErrorMessage = "El teléfono no puede superar los 30 caracteres."), Telefono]
    public required string Telefono { get; init; }

    [Required, MinLength(1, ErrorMessage = "El usuario debe tener al menos un rol.")]
    public required List<long> IdsRol { get; init; }

    [Required]
    public required string Estado { get; init; }

    public required DateOnly FechaIngreso { get; init; }
}
