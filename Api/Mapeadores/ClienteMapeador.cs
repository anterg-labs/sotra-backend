using Api.Validaciones;
using Api.DTOs.Clientes;
using SOTRA_BACKEND.Modelos;

namespace Api.Mapeadores;

public static class ClienteMapeador
{
    public static Cliente AEntidad(ClienteCrearDto cliente)
    {
        string? correo = cliente.Correo;
        if (string.IsNullOrWhiteSpace(correo))
        {
            correo = null;
        }

        string? telefono = cliente.Telefono;
        if (string.IsNullOrWhiteSpace(telefono))
        {
            telefono = null;
        }
        else
        {
            telefono = NumerosDeTelefono.Normalizar(telefono);
        }

        string? direccion = cliente.Direccion;
        if (string.IsNullOrWhiteSpace(direccion))
        {
            direccion = null;
        }

        string? observaciones = cliente.Observaciones;
        if (string.IsNullOrWhiteSpace(observaciones))
        {
            observaciones = null;
        }

        return new Cliente
        {
            RazonSocial = cliente.RazonSocial,
            Nombre = cliente.NombreContacto,
            Correo = correo,
            Telefono = telefono,
            Direccion = direccion,
            Observaciones = observaciones,
            Estado = true,
            FechaIngreso = DateOnly.FromDateTime(DateTime.UtcNow)
        };
    }

    public static void AplicarCambios(Cliente destino, ClienteActualizarDto cambios)
    {
        destino.RazonSocial = cambios.RazonSocial;
        destino.Nombre = cambios.NombreContacto;

        destino.Correo = cambios.Correo;
        if (string.IsNullOrWhiteSpace(destino.Correo))
        {
            destino.Correo = null;
        }

        destino.Telefono = string.IsNullOrWhiteSpace(cambios.Telefono) ? null : NumerosDeTelefono.Normalizar(cambios.Telefono);

        destino.Direccion = cambios.Direccion;
        if (string.IsNullOrWhiteSpace(destino.Direccion))
        {
            destino.Direccion = null;
        }

        destino.Observaciones = cambios.Observaciones;
        if (string.IsNullOrWhiteSpace(destino.Observaciones))
        {
            destino.Observaciones = null;
        }
    }

    public static ClienteListadoDto AListadoDto(Cliente cliente)
        => AListadoDto(cliente, 0);

    public static ClienteListadoDto AListadoDto(Cliente cliente, int cantidadOrdenesAsociadas)
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
            CantidadOrdenesAsociadas = cantidadOrdenesAsociadas,
            Estado = cliente.Estado ? "activo" : "inactivo",
            CreadoEn = cliente.FechaIngreso
        };
    }
}
