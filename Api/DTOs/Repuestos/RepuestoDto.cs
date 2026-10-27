namespace Api.DTOs.Repuestos;

public class RepuestoDto
{
    public long Id { get; init; }
    public required string Codigo { get; init; }
    public required string Nombre { get; init; }
    public required string Descripcion { get; init; }
    public required string Marca { get; init; }
    public required string Ubicacion { get; init; }
    public int Cantidad { get; init; }
    public int CantidadMinima { get; init; }

    /// <summary>Unidades reservadas por órdenes que todavía no empezaron; siguen en la empresa pero no se pueden asignar a otra orden.</summary>
    public int CantidadReservada { get; init; }

    /// <summary>Unidades entregadas a órdenes en curso; se descuentan del stock al cerrar la orden.</summary>
    public int CantidadEntregada { get; init; }

    /// <summary>Lo que se puede asignar a una orden nueva: el stock menos lo reservado y lo entregado.</summary>
    public int CantidadDisponible { get; init; }
    public required string Estado { get; init; }
    public required string EstadoStock { get; init; }
}
