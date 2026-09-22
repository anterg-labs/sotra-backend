using System.Linq.Expressions;
using Api.DTOs.Clientes;
using SOTRA_BACKEND.Modelos;

namespace Api.Mapeadores;

public static class ClienteMapeador
{
    public static Expression<Func<Cliente, ClienteListadoDto>> Proyeccion => cliente =>
        new ClienteListadoDto
        {
            Id = cliente.IdCliente,
            RazonSocial = cliente.RazonSocial,
            NombreContacto = cliente.Nombre,
            Correo = cliente.Correo,
            Telefono = cliente.Telefono,
            Direccion = cliente.Direccion,
            Observaciones = cliente.Observaciones,
            CantidadActivosAsociados = cliente.Activos.Count,
            Estado = cliente.Estado ? "activo" : "inactivo",
            CreadoEn = cliente.FechaIngreso
        };

    public static Cliente AEntidad(ClienteCrearDto cliente)
    {
        return new Cliente
        {
            RazonSocial = cliente.RazonSocial,
            Nombre = cliente.NombreContacto,
            Correo = cliente.Correo,
            Telefono = cliente.Telefono,
            Direccion = cliente.Direccion,
            Observaciones = cliente.Observaciones,
            Estado = true,
            FechaIngreso = DateOnly.FromDateTime(DateTime.UtcNow)
        };
    }

    public static void AplicarCambios(Cliente destino, ClienteActualizarDto cambios)
    {
        if (!string.IsNullOrWhiteSpace(cambios.RazonSocial))
            destino.RazonSocial = cambios.RazonSocial;
        if (!string.IsNullOrWhiteSpace(cambios.NombreContacto))
            destino.Nombre = cambios.NombreContacto;
        if (!string.IsNullOrWhiteSpace(cambios.Correo))
            destino.Correo = cambios.Correo;
        if (!string.IsNullOrWhiteSpace(cambios.Telefono))
            destino.Telefono = cambios.Telefono;
        if (!string.IsNullOrWhiteSpace(cambios.Direccion))
            destino.Direccion = cambios.Direccion;
        if (!string.IsNullOrWhiteSpace(cambios.Observaciones))
            destino.Observaciones = cambios.Observaciones;
    }

    public static ClienteListadoDto AListadoDto(Cliente cliente)
    {
        return new ClienteListadoDto
        {
            Id = cliente.IdCliente,
            RazonSocial = cliente.RazonSocial,
            NombreContacto = cliente.Nombre,
            Correo = cliente.Correo,
            Telefono = cliente.Telefono,
            Direccion = cliente.Direccion,
            Observaciones = cliente.Observaciones,
            CantidadActivosAsociados = cliente.Activos.Count,
            Estado = cliente.Estado ? "activo" : "inactivo",
            CreadoEn = cliente.FechaIngreso
        };
    }
}
