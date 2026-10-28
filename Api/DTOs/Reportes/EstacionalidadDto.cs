namespace Api.DTOs.Reportes;

/// <summary>Correctivas creadas por mes del año; los años del período se acumulan.</summary>
public sealed record MesEstacionalidadDto(int Mes, int Correctivas);

/// <summary>Una fila del mapa de calor, con doce meses y su total.</summary>
public sealed record TipoEstacionalidadDto(long TipoMaquinaId, string TipoMaquina, int Total, List<MesEstacionalidadDto> Meses);

/// <summary>Distribución mensual de correctivas por tipo de máquina. No es una predicción.</summary>
public sealed record EstacionalidadDto(DateOnly? FechaDesde, DateOnly? FechaHasta, int Total,
    List<MesEstacionalidadDto> Meses, List<TipoEstacionalidadDto> Tipos);
