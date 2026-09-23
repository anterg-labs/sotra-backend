using System.Linq.Expressions;
using Api.DTOs.Usuarios;
using SOTRA_BACKEND.Modelos;

namespace Api.Mapeadores;

public static class UsuarioMapeador
{
    public static Expression<Func<Usuario, UsuarioListadoDto>> Proyeccion => usuario => new UsuarioListadoDto
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
