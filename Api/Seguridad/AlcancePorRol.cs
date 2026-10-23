using System.Security.Claims;
using SOTRA_BACKEND.Modelos;

namespace Api.Seguridad;

// Reglas que dependen de quién pide, además de la política del endpoint.
// Sin token (AUTH_OBLIGATORIA=false) no hay usuario y no se restringe nada.
public static class AlcancePorRol
{
    // Técnico sin otro rol de gestión: solo trabaja sobre las órdenes que tiene asignadas.
    public static bool EsSoloTecnico(this ClaimsPrincipal usuario)
    {
        return usuario.Identity?.IsAuthenticated == true
               && usuario.IsInRole(Roles.Tecnico)
               && !usuario.IsInRole(Roles.Administrador)
               && !usuario.IsInRole(Roles.JefeMantenimiento)
               && !usuario.IsInRole(Roles.Panolero);
    }

    // El jefe de mantenimiento gestiona usuarios, pero no administradores: no puede asignar
    // ese rol ni modificar a quien lo tiene. Así no puede darse acceso total.
    public static bool PuedeGestionarAdministradores(this ClaimsPrincipal usuario)
    {
        return usuario.Identity?.IsAuthenticated != true || usuario.IsInRole(Roles.Administrador);
    }

    public static async Task<bool> PuedeVerOrdenAsync(this ClaimsPrincipal usuario, OrdenTrabajo orden,
        UsuarioActual usuarioActual, CancellationToken cancelacion)
    {
        if (!usuario.EsSoloTecnico())
        {
            return true;
        }

        var idUsuario = await usuarioActual.ObtenerIdUsuarioAsync(cancelacion);
        return idUsuario is not null && orden.IdUsuarioTecnico == idUsuario;
    }
}
