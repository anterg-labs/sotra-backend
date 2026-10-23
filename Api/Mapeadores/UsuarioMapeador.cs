using Api.DTOs.Usuarios;
using SOTRA_BACKEND.Modelos;

namespace Api.Mapeadores;

public static class UsuarioMapeador
{
    public static Usuario AEntidad(UsuarioCrearDto usuario)
    {
        return new Usuario
        {
            Nombre = usuario.Nombre,
            Apellido = usuario.Apellido,
            Mail = usuario.Correo,
            Telefono = usuario.Telefono,
            Estado = true,
            FechaIngreso = DateOnly.FromDateTime(DateTime.UtcNow)
        };
    }

    public static void AplicarCambios(Usuario destino, UsuarioActualizarDto cambios)
    {
        destino.Nombre = cambios.Nombre;
        destino.Apellido = cambios.Apellido;
        destino.Mail = cambios.Correo;
        destino.Telefono = cambios.Telefono;
        destino.FechaIngreso = cambios.FechaIngreso;

        if (cambios.Estado == "activo")
        {
            destino.Estado = true;
        }
        else if (cambios.Estado == "inactivo")
        {
            destino.Estado = false;
        }
        else
        {
            throw new ArgumentException("El estado debe ser 'activo' o 'inactivo'.");
        }
    }

    public static UsuarioListadoDto AListadoDto(Usuario usuario)
    {
        return new UsuarioListadoDto
        {
            Id = usuario.IdUsuario,
            Nombre = usuario.Nombre,
            Apellido = usuario.Apellido,
            Correo = usuario.Mail,
            Telefono = usuario.Telefono,
            Estado = usuario.Estado ? "activo" : "inactivo",
            FechaIngreso = usuario.FechaIngreso,
            Roles = usuario.IdRols.OrderBy(rol => rol.IdRol).Select(rol => new RolUsuarioDto
            {
                Id = rol.IdRol,
                Nombre = rol.Nombre,
                Activo = rol.Activo
            }).ToList()
        };
    }
}
