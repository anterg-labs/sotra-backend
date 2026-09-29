using Api.DTOs.Roles;
using SOTRA_BACKEND.Modelos;

namespace Api.Mapeadores;

public static class RolMapeador
{
    public static RolDto ADto(Rol rol)
    {
        return new RolDto
        {
            Id = rol.IdRol,
            Nombre = rol.Nombre
        };
    }
}
