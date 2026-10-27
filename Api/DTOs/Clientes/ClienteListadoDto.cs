namespace Api.DTOs.Clientes;

public class ClienteListadoDto
{
    /// <summary>Identificador del registro.</summary>
    public long Id { get; init; }
    /// <summary>Razón social del cliente.</summary>
    public required string RazonSocial { get; init; }
    /// <summary>Nombre de la persona de contacto.</summary>
    public required string NombreContacto { get; init; }
    /// <summary>Correo electrónico.</summary>
    public string? Correo { get; init; }
    /// <summary>Teléfono o celular (8 dígitos, o 9 con cero inicial).</summary>
    public string? Telefono { get; init; }
    /// <summary>Dirección.</summary>
    public string? Direccion { get; init; }
    /// <summary>Observaciones adicionales, opcionales.</summary>
    public string? Observaciones { get; init; }
    /// <summary>Cantidad de activos del cliente.</summary>
    public int CantidadActivosAsociados { get; init; }
    /// <summary>Cantidad de órdenes activas del cliente (pendientes, en curso o por validar).</summary>
    public int CantidadOrdenesAsociadas { get; init; }
    /// <summary>Estado del registro (`activo` o `inactivo`) o, en órdenes, valor del estado vigente del catálogo.</summary>
    public required string Estado { get; init; }
    /// <summary>Fecha y hora de creación (ISO 8601, UTC).</summary>
    public DateOnly CreadoEn { get; init; }
}
