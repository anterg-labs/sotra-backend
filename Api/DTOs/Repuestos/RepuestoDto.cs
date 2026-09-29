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
    public required string Estado { get; init; }
}
