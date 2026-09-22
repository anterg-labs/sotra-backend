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

    public static Usuario AEntidad(UsuarioCrearDto usuario)
    {
        return new Usuario
        {
            Nombre = usuario.Nombre,
            Apellido = usuario.Apellido,
            Mail = usuario.Correo,
            Telefono = usuario.Telefono,
            Contrasena = usuario.Contrasena,
            Estado = true,
            FechaIngreso = DateOnly.FromDateTime(DateTime.UtcNow)
        };
    }

    public static void AplicarCambios(Usuario destino, UsuarioActualizarDto cambios)
    {
        if (!string.IsNullOrWhiteSpace(cambios.Nombre)) destino.Nombre = cambios.Nombre;
        if (!string.IsNullOrWhiteSpace(cambios.Apellido)) destino.Apellido = cambios.Apellido;
        if (!string.IsNullOrWhiteSpace(cambios.Correo)) destino.Mail = cambios.Correo;
        if (!string.IsNullOrWhiteSpace(cambios.Telefono)) destino.Telefono = cambios.Telefono;
        if (!string.IsNullOrWhiteSpace(cambios.Estado)) destino.Estado = EstadoABooleano(cambios.Estado);
        if (cambios.FechaIngreso.HasValue) destino.FechaIngreso = cambios.FechaIngreso.Value;
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

    private static bool EstadoABooleano(string estado)
    {
        return estado.Trim().ToLowerInvariant() switch
        {
            "activo" => true,
            "inactivo" => false,
            _ => throw new ArgumentException("El estado debe ser 'activo' o 'inactivo'.", nameof(estado))
        };
    }
}
