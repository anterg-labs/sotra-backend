namespace Api.DTOs.Repuestos;

public class RepuestoDto
{
    /// <summary>Identificador del registro.</summary>
    public long Id { get; init; }
    /// <summary>Código.</summary>
    public required string Codigo { get; init; }
    /// <summary>Nombre.</summary>
    public required string Nombre { get; init; }
    /// <summary>Descripción.</summary>
    public required string Descripcion { get; init; }
    /// <summary>Marca.</summary>
    public required string Marca { get; init; }
    /// <summary>Ubicación o depósito.</summary>
    public required string Ubicacion { get; init; }
    /// <summary>Cantidad (unidades, órdenes u otro conteo según el contexto).</summary>
    public int Cantidad { get; init; }
    /// <summary>Stock mínimo antes de alertar.</summary>
    public int CantidadMinima { get; init; }

    /// <summary>Unidades reservadas por órdenes que todavía no empezaron; siguen en la empresa pero no se pueden asignar a otra orden.</summary>
    public int CantidadReservada { get; init; }

    /// <summary>Unidades entregadas a órdenes en curso; se descuentan del stock al cerrar la orden.</summary>
    public int CantidadEntregada { get; init; }

    /// <summary>Lo que se puede asignar a una orden nueva: el stock menos lo reservado y lo entregado.</summary>
    public int CantidadDisponible { get; init; }
    /// <summary>Estado del registro (`activo` o `inactivo`) o, en órdenes, valor del estado vigente del catálogo.</summary>
    public required string Estado { get; init; }
    /// <summary>Estado del stock: `sin_stock`, `stock_bajo` o `normal`.</summary>
    public required string EstadoStock { get; init; }

    /// <summary>Costo unitario, si se cargó.</summary>
    public decimal? CostoUnitario { get; init; }
}
