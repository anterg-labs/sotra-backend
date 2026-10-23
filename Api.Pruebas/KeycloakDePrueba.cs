using Api.Servicios;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Pruebas;

// Reemplaza la administración de usuarios de Keycloak: las pruebas no levantan Keycloak.
// Cada alta devuelve un id nuevo, como si Keycloak hubiera creado el usuario.
public class KeycloakDePrueba() : ServicioKeycloakUsuarios(NullLogger<ServicioKeycloakUsuarios>.Instance)
{
    public override Task<(Guid Id, bool Creado)> CrearOObtenerAsync(DatosUsuarioKeycloak datos,
        CancellationToken cancellationToken)
    {
        return Task.FromResult((Guid.NewGuid(), true));
    }

    public override Task ActualizarAsync(Guid id, DatosUsuarioKeycloak datos, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public override Task ReemplazarRolesAsync(Guid id, IEnumerable<string> rolesDeseados,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public override Task EnviarCorreoParaElegirContrasenaAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public override Task CerrarSesionesAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public override Task EliminarSinErrorAsync(Guid id, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
