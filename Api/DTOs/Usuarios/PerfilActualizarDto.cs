using System.ComponentModel.DataAnnotations;
using Api.Validaciones;

namespace Api.DTOs.Usuarios;

/// <summary>Datos que una persona puede cambiar de su propio perfil sin depender de Keycloak: nombre, apellido y teléfono.</summary>
public class PerfilActualizarDto
{
    /// <summary>Nombre.</summary>
    [Required(ErrorMessage = "El campo Nombre es obligatorio."), MaxLength(100, ErrorMessage = "El campo Nombre no puede superar los 100 caracteres."), NombrePersona]
    public required string Nombre { get; init; }

    /// <summary>Apellido.</summary>
    [Required(ErrorMessage = "El campo Apellido es obligatorio."), MaxLength(100, ErrorMessage = "El campo Apellido no puede superar los 100 caracteres."), NombrePersona]
    public required string Apellido { get; init; }

    /// <summary>Teléfono o celular (8 dígitos, o 9 con cero inicial).</summary>
    [Required(ErrorMessage = "El campo teléfono es obligatorio."), MaxLength(30, ErrorMessage = "El teléfono no puede superar los 30 caracteres."), Telefono]
    public required string Telefono { get; init; }
}
