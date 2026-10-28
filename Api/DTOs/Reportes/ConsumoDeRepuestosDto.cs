namespace Api.DTOs.Reportes;

public sealed record RepuestoConsumidoDto(long RepuestoId, string Codigo, string Repuesto,
    long Unidades, int Ordenes);

/// <summary>Consumo físico de repuestos por egresos asociados al cierre de órdenes.</summary>
public sealed record ConsumoDeRepuestosDto(DateOnly? FechaDesde, DateOnly? FechaHasta,
    long TotalUnidades, List<RepuestoConsumidoDto> Repuestos);
