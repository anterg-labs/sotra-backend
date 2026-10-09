namespace Api.DTOs.MovimientosStock;

public class MovimientoStockDto
{
    public long Id { get; init; }
    public long RepuestoId { get; init; }
    public required string CodigoRepuesto { get; init; }
    public required string NombreRepuesto { get; init; }
    public required string Tipo { get; init; }
    public int Cantidad { get; init; }
    public DateTime Fecha { get; init; }
    public string Ubicacion { get; init; } = "";
    public long? UsuarioId { get; init; }
    public string? NombreUsuario { get; init; }
    public string Motivo { get; init; } = "";
    public string Observaciones { get; init; } = "";
    public int? StockAnterior { get; init; }
    public int? StockPosterior { get; init; }
}
