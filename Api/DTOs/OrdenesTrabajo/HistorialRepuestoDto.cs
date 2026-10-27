namespace Api.DTOs.OrdenesTrabajo;

/// <summary>Un cambio de un repuesto en la orden: qué se hizo, con cuántas unidades, quién y cuándo.</summary>
public class HistorialRepuestoDto
{
    public long RepuestoId { get; init; }
    public required string Codigo { get; init; }
    public required string Nombre { get; init; }

    /// <summary><c>reservar</c>, <c>modificar</c>, <c>entregar</c>, <c>descontar</c> o <c>liberar</c>.</summary>
    public required string Accion { get; init; }

    public int? CantidadAnterior { get; init; }
    public int CantidadNueva { get; init; }
    public string? EstadoAnterior { get; init; }
    public required string EstadoNuevo { get; init; }
    public DateTime FechaHora { get; init; }
    public long? UsuarioId { get; init; }
    public string? NombreUsuario { get; init; }
}
