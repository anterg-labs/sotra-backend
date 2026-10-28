namespace Api.DTOs.Reportes;

/// <summary>Demanda de mantenimiento de una empresa cliente en el período.</summary>
public sealed record ClienteDeReportesDto(long ClienteId, string Cliente, int Ordenes,
    int Correctivas, int CorrectivasCerradas, double HorasParada, int Reincidencias);

/// <summary>Agregado por empresa, sin nombres de personas.</summary>
public sealed record ResumenPorClienteDto(DateOnly? FechaDesde, DateOnly? FechaHasta,
    int DiasReincidencia, List<ClienteDeReportesDto> Clientes);
