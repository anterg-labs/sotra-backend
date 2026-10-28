namespace Api.DTOs.Reportes;

public sealed record ActivoEdadUsoDto(long ActivoId, string NombreActivo, string Modelo,
    int? EdadAnios, decimal? LecturaUso, string UnidadUso, DateTime? FechaLectura,
    int Correctivas, int OrdenesObservadas);

/// <summary>Correctivas frente a edad y última lectura conocida de uso por activo observado.</summary>
public sealed record DesgasteEdadUsoDto(DateOnly? FechaDesde, DateOnly? FechaHasta,
    int TotalActivos, int ConEdad, int ConUso, List<ActivoEdadUsoDto> Activos);
