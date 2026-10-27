namespace Api.DTOs.OrdenesTrabajo;

public class RepuestoOrdenDto
{
    public long Id { get; init; }
    public required string Codigo { get; init; }
    public required string Nombre { get; init; }

    /// <summary>Unidades del repuesto que usa la orden.</summary>
    public int Cantidad { get; init; }

    /// <summary><c>reservado</c>, <c>entregado</c> o <c>descontado</c>; los liberados no se listan.</summary>
    public required string Estado { get; init; }
}
