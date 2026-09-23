using System.Linq.Expressions;
using Api.DTOs.Roles;
using SOTRA_BACKEND.Modelos;

namespace Api.Mapeadores;

public static class RolMapeador
{
    public static Expression<Func<Rol, RolDto>> Proyeccion => rol =>
        new RolDto
        {
            Id = rol.IdRol,
            Nombre = rol.Nombre
        };
}
