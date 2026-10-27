namespace Api.DTOs.OrdenesTrabajo;

/// <summary>Un estado del catálogo de órdenes (tabla <c>estado_ot</c>).</summary>
/// <param name="Valor">Valor del estado, igual en el servidor y en la interfaz (por ejemplo <c>en_progreso</c>).</param>
/// <param name="Etiqueta">Texto que se muestra a la persona usuaria.</param>
/// <param name="Orden">Posición del estado en las listas.</param>
/// <param name="Fase">Fase que agrupa estados: <c>pendiente</c>, <c>en_curso</c>, <c>por_validar</c>, <c>cerrada</c> o <c>eliminada</c>.</param>
/// <param name="Tono">Color de la etiqueta en la interfaz.</param>
public record EstadoOrdenTrabajoDto(string Valor, string Etiqueta, int Orden, string Fase, string Tono);
