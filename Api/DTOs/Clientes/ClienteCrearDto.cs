using System.Text.Json.Serialization;
using Api.Infraestructura;
using System.ComponentModel.DataAnnotations;
using Api.Validaciones;

namespace Api.DTOs.Clientes;

public class ClienteCrearDto
{
    [Required(ErrorMessage = "El campo Nombre o razón social es obligatorio."), MaxLength(160, ErrorMessage = "El campo Nombre o razón social no puede superar los 160 caracteres.")]
    public required string RazonSocial { get; init; }

    [Required(ErrorMessage = "El campo Nombre de contacto es obligatorio."), MaxLength(120, ErrorMessage = "El campo Nombre de contacto no puede superar los 120 caracteres.")]
    public required string NombreContacto { get; init; }

    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido."), MaxLength(254, ErrorMessage = "El correo no puede superar los 254 caracteres.")]
    [JsonConverter(typeof(TextoOpcionalJsonConverter))]
    public string? Correo { get; init; }

    [MaxLength(30, ErrorMessage = "El teléfono no puede superar los 30 caracteres."), Telefono]
    public string? Telefono { get; init; }

    [MaxLength(255, ErrorMessage = "La dirección no puede superar los 255 caracteres.")]
    public string? Direccion { get; init; }

    public string? Observaciones { get; init; }
}
