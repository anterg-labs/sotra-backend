using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.Clientes;

public class ClienteActualizarDto
{
    [Required, MaxLength(160)]
    public required string RazonSocial { get; init; }

    [Required, MaxLength(120)]
    public required string NombreContacto { get; init; }

    [EmailAddress, MaxLength(254)]
    public string? Correo { get; init; }

    [MaxLength(30)]
    public string? Telefono { get; init; }

    [MaxLength(255)]
    public string? Direccion { get; init; }

    public string? Observaciones { get; init; }
}
