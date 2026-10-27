using System.ComponentModel.DataAnnotations;
using Api.Validaciones;

namespace Api.DTOs.Usuarios;

public class UsuarioActualizarDto
{
    /// <summary>Nombre.</summary>
    [Required(ErrorMessage = "El campo Nombre es obligatorio."), MaxLength(100, ErrorMessage = "El campo Nombre no puede superar los 100 caracteres."), NombrePersona]
    public required string Nombre { get; init; }

    /// <summary>Apellido.</summary>
    [Required(ErrorMessage = "El campo Apellido es obligatorio."), MaxLength(100, ErrorMessage = "El campo Apellido no puede superar los 100 caracteres."), NombrePersona]
    public required string Apellido { get; init; }

    /// <summary>Correo electrónico.</summary>
    [Required(ErrorMessage = "El campo Correo es obligatorio."), EmailAddress(ErrorMessage = "El correo no tiene un formato válido."), MaxLength(254, ErrorMessage = "El correo no puede superar los 254 caracteres.")]
    public required string Correo { get; init; }

    /// <summary>Teléfono o celular (8 dígitos, o 9 con cero inicial).</summary>
    [Required(ErrorMessage = "El campo teléfono es obligatorio."), MaxLength(30, ErrorMessage = "El teléfono no puede superar los 30 caracteres."), Telefono]
    public required string Telefono { get; init; }

    /// <summary>Identificadores de los roles asignados (al menos uno).</summary>
    [Required, MinLength(1, ErrorMessage = "El usuario debe tener al menos un rol.")]
    public required List<long> IdsRol { get; init; }

    /// <summary>Estado del registro (`activo` o `inactivo`) o, en órdenes, valor del estado vigente del catálogo.</summary>
    [Required]
    public required string Estado { get; init; }

    /// <summary>Fecha de ingreso (`aaaa-mm-dd`).</summary>
    public required DateOnly FechaIngreso { get; init; }
}
