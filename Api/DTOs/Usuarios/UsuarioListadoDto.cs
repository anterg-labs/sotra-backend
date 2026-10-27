using System.Text.Json.Serialization;

namespace Api.DTOs.Usuarios;

public class UsuarioListadoDto
{
    /// <summary>Identificador del registro.</summary>
    public long Id { get; init; }


    /// <summary>Nombre.</summary>
    public required string Nombre { get; init; }

    /// <summary>Apellido.</summary>
    public required string Apellido { get; init; }

    /// <summary>Correo electrónico.</summary>
    public required string Correo { get; init; }

    /// <summary>Teléfono o celular (8 dígitos, o 9 con cero inicial).</summary>
    public required string Telefono { get; init; }

    /// <summary>Estado del registro (`activo` o `inactivo`) o, en órdenes, valor del estado vigente del catálogo.</summary>
    public required string Estado { get; init; }

    /// <summary>Fecha de ingreso (`aaaa-mm-dd`).</summary>
    public DateOnly FechaIngreso { get; init; }
    /// <summary>Cantidad de órdenes activas asignadas al técnico.</summary>
    public int CantidadOrdenesAsignadas { get; init; }

    /// <summary>
    /// Solo en la respuesta del alta: <c>true</c> si Keycloak envió el correo para elegir la contraseña, <c>false</c> si no
    /// pudo (el usuario queda creado y se puede reenviar), <c>null</c> si no corresponde (el usuario ya tenía acceso).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? CorreoDeContrasenaEnviado { get; init; }

    /// <summary>Roles asignados.</summary>
    public List<RolUsuarioDto> Roles { get; init; } = [];
}
