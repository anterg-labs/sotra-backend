namespace Api.DTOs.Reportes;

/// <summary>Frecuencia de un repuesto vinculado a correctivas de un modelo.</summary>
public sealed record RepuestoDeModeloDto(long RepuestoId, string Codigo, string Repuesto,
    int OrdenesAsociadas, int OrdenesSuministradas);

/// <summary>Modelo identificado también por su tipo para evitar confundir nombres iguales.</summary>
public sealed record ModeloConRepuestosDto(long ModeloId, string Modelo, long TipoMaquinaId,
    string TipoMaquina, List<RepuestoDeModeloDto> Repuestos);

/// <summary>Asociaciones vigentes de repuestos a órdenes correctivas, no unidades consumidas.</summary>
public sealed record RepuestosPorModeloDto(DateOnly? FechaDesde, DateOnly? FechaHasta,
    List<ModeloConRepuestosDto> Modelos);
