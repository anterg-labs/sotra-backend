using System.Text.Json.Serialization;

namespace Api.DTOs.Usuarios;

public class UsuarioListadoDto
{
    public long Id { get; init; }


    public required string Nombre { get; init; }

    public required string Apellido { get; init; }

    public required string Correo { get; init; }

    public required string Telefono { get; init; }

    public required string Estado { get; init; }

    public DateOnly FechaIngreso { get; init; }
    public int CantidadOrdenesAsignadas { get; init; }

    /// <summary>
    /// Solo en la respuesta del alta: <c>true</c> si Keycloak envió el correo para elegir la contraseña, <c>false</c> si no
    /// pudo (el usuario queda creado y se puede reenviar), <c>null</c> si no corresponde (el usuario ya tenía acceso).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? CorreoDeContrasenaEnviado { get; init; }

    public List<RolUsuarioDto> Roles { get; init; } = [];
}
