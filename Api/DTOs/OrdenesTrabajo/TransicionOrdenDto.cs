namespace Api.DTOs.OrdenesTrabajo;

/// <summary>Un cambio de estado posible desde el estado actual de la orden, tal como lo define la base.</summary>
public record TransicionOrdenDto(string Estado, bool ExigeMotivo, bool PuedeTecnico, string? Accion, bool Principal);
