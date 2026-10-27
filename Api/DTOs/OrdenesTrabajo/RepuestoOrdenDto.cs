namespace Api.DTOs.OrdenesTrabajo;

public class RepuestoOrdenDto
{
    /// <summary>Identificador del registro.</summary>
    public long Id { get; init; }
    /// <summary>Código.</summary>
    public required string Codigo { get; init; }
    /// <summary>Nombre.</summary>
    public required string Nombre { get; init; }

    /// <summary>Unidades del repuesto que usa la orden.</summary>
    public int Cantidad { get; init; }

    /// <summary><c>reservado</c>, <c>entregado</c> o <c>descontado</c>; los liberados no se listan.</summary>
    public required string Estado { get; init; }
}
