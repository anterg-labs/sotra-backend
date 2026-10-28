using Api.DTOs;

namespace Api.DTOs.Reportes;

public class UmbralEtapaDto
{
    public required string Etapa { get; init; }
    public double? UmbralHoras { get; init; }
}

public class ActualizarUmbralEtapaDto
{
    // Required distingue restablecer a null de omitir accidentalmente la propiedad.
    public required double? UmbralHoras { get; init; }
}

public class EstadisticaEtapaDto
{
    public required string Etapa { get; init; }
    public required string Nombre { get; init; }
    public int CantidadOrdenes { get; init; }
    public double? PromedioHoras { get; init; }
    public double? MedianaHoras { get; init; }
    public double? Percentil90Horas { get; init; }
    public double? PromedioHistoricoHoras { get; init; }
    public int MuestraHistorica { get; init; }
    public double? UmbralHoras { get; init; }
    public required string OrigenUmbral { get; init; }
    public int Demoradas { get; init; }
}

public class OrdenDemoradaEtapaDto
{
    public long OrdenId { get; init; }
    public long ActivoId { get; init; }
    public required string NombreActivo { get; init; }
    public required string Etapa { get; init; }
    public double Horas { get; init; }
    public double UmbralHoras { get; init; }
    public double ExcesoHoras { get; init; }
    public bool EnCurso { get; init; }
}

public class DemorasEtapasDto
{
    public DateTime CalculadoEn { get; init; }
    public required List<EstadisticaEtapaDto> Etapas { get; init; }
    public required PaginaDto<OrdenDemoradaEtapaDto> Demoradas { get; init; }
}
