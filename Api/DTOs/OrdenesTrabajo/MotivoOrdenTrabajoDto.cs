namespace Api.DTOs.OrdenesTrabajo;

/// <summary>Motivo codificado de una pausa o devolución.</summary>
/// <param name="Codigo">Código estable (`falta_repuesto`, `esperando_autorizacion`, `esperando_cliente` u `otro`).</param>
/// <param name="Descripcion">Texto para mostrar.</param>
public record MotivoOrdenTrabajoDto(string Codigo, string Descripcion);
