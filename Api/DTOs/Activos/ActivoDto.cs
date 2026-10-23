namespace Api.DTOs.Activos;

public class ActivoDto
{
    public long Id { get; init; }
    public long ClienteId { get; init; }
    public string? NombreCliente { get; init; }
    public required string NumSerie { get; init; }
    public string? Descripcion { get; init; }
    public long ModeloMaquinaId { get; init; }
    public string? NombreModeloMaquina { get; init; }
    public short? AnioFabricacion { get; init; }
    public string? Nombre { get; init; }
    public string? Ubicacion { get; init; }
    public required string Estado { get; init; }
}
