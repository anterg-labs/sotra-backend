namespace Api.DTOs.OrdenesTrabajo;

/// <summary>Un estado del catálogo de órdenes (tabla <c>estado_ot</c>).</summary>
public record EstadoOrdenTrabajoDto(string Valor, string Etiqueta, int Orden, string Fase, string Tono);
