namespace Api.DTOs.OrdenesTrabajo;

/// <summary>Un cambio de estado posible desde el estado actual de la orden, tal como lo define la base.</summary>
/// <param name="Estado">Estado al que se puede pasar.</param>
/// <param name="ExigeMotivo">Si el cambio necesita un motivo escrito.</param>
/// <param name="PuedeTecnico">Si el técnico asignado puede hacer este cambio.</param>
/// <param name="Accion">Texto del botón que ejecuta el cambio.</param>
/// <param name="Principal">Si es la acción principal que se destaca en la interfaz.</param>
public record TransicionOrdenDto(string Estado, bool ExigeMotivo, bool PuedeTecnico, string? Accion, bool Principal);
