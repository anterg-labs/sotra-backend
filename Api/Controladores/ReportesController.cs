using Api.DTOs.OrdenesTrabajo;
using Api.Infraestructura;
using Api.DTOs.Reportes;
using Api.Mapeadores;
using Api.Seguridad;
using Api.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

[ApiController]
[Route("api/v1/reportes")]
public class ReportesController : ControllerBase
{
    private const int CantidadDeActivosEnRanking = 5;
    private const int CantidadDeOrdenesRecientes = 5;

    /// <summary>Máximo de filas del listado de órdenes en un reporte; los demás cuadros ya son resúmenes agrupados.</summary>
    internal const int MaximoDeOrdenesListadas = 5_000;

    private readonly OrdenTrabajoDAO _ordenTrabajoDao;
    private readonly RepuestoDAO _repuestoDao;

    public ReportesController(OrdenTrabajoDAO ordenTrabajoDao, RepuestoDAO repuestoDao)
    {
        _ordenTrabajoDao = ordenTrabajoDao;
        _repuestoDao = repuestoDao;
    }

    [HttpGet("indicadores")]
    [Authorize(Policy = Politicas.ReporteConsultar)]
    public async Task<ActionResult<IndicadoresDto>> ObtenerIndicadores([FromQuery] FiltrosIndicadoresDto filtros, CancellationToken cancelacion)
    {
        if (RangoInvertido(filtros))
        {
            return RangoDeFechasInvalido();
        }

        var filtro = AFiltroDeOrdenes(filtros);
        var ordenes = TipoValido(filtros) ? await _ordenTrabajoDao.ListarResumenesAsync(filtro, cancelacion) : [];

        var horasHastaInicio = ordenes.Where(o => o.FechaInicio is not null)
            .Select(o => (o.FechaInicio!.Value - o.CreadoEn).TotalHours).ToList();
        var horasHastaCierre = ordenes.Where(o => o.FechaCierre is not null)
            .Select(o => (o.FechaCierre!.Value - o.CreadoEn).TotalHours).ToList();

        var reglas = await _ordenTrabajoDao.ObtenerReglasAsync(cancelacion);
        var ordenesRecientes = ordenes.Count == 0
            ? []
            : (await _ordenTrabajoDao.ListarPaginaAsync(filtro with { OrdenarPorCreacion = true, Descendente = true }, 1,
                CantidadDeOrdenesRecientes, cancelacion)).Elementos.Select(orden => OrdenTrabajoMapeador.ADto(orden, reglas)).ToList();

        return Ok(new IndicadoresDto
        {
            TotalOrdenes = ordenes.Count,
            OrdenesPorEstado = Agrupar(ordenes, o => o.Estado),
            OrdenesPorPrioridad = Agrupar(ordenes, o => OrdenTrabajoMapeador.APrioridadTexto(o.Prioridad)),
            OrdenesPorTipo = Agrupar(ordenes, TextoDelTipo),
            OrdenesPorTecnico = Agrupar(ordenes.Where(o => o.Tecnico is not null), o => o.Tecnico!),
            ActivosConMasIntervenciones = Agrupar(ordenes, o => o.Activo)
                .OrderByDescending(grupo => grupo.Cantidad).Take(CantidadDeActivosEnRanking).ToList(),
            RepuestosBajoStock = (await _repuestoDao.ListarConStockInsuficienteAsync(cancelacion)).Select(RepuestoMapeador.ADto).ToList(),
            PromedioHorasHastaInicio = CalcularPromedio(horasHastaInicio),
            PromedioHorasHastaCierre = CalcularPromedio(horasHastaCierre),
            OrdenesAltaPrioridadPendientes = ordenes.Count(o => o.Prioridad >= 5 && !EstaTerminada(reglas, o.Estado)),
            OrdenesPendientesDeValidacion = ordenes.Count(o => reglas.FaseDe(o.Estado) == FasesDeEstado.PorValidar),
            OrdenesRecientes = ordenesRecientes
        });
    }

    [HttpGet("{tipoReporte}/pdf")]
    [Authorize(Policy = Politicas.ReporteConsultar)]
    public async Task<IActionResult> DescargarReportePdf(
        string tipoReporte, [FromQuery] FiltrosIndicadoresDto filtros, CancellationToken cancelacion)
    {
        var resultado = await ArmarReporte(tipoReporte, filtros, cancelacion);
        if (resultado.Error is not null)
        {
            return resultado.Error;
        }

        var reporte = resultado.Reporte!;
        var archivo = GeneradorReportesPdf.Generar(reporte.Titulo, reporte.Criterios, reporte.Tablas);
        MetricasDeNegocio.ReportesGenerados.Add(1, new KeyValuePair<string, object?>("tipo", tipoReporte),
            new KeyValuePair<string, object?>("formato", "pdf"));
        return File(archivo, "application/pdf", "reporte-" + tipoReporte + ".pdf");
    }

    [HttpGet("{tipoReporte}/csv")]
    [Authorize(Policy = Politicas.ReporteConsultar)]
    public async Task<IActionResult> DescargarReporteCsv(
        string tipoReporte, [FromQuery] FiltrosIndicadoresDto filtros, CancellationToken cancelacion)
    {
        var resultado = await ArmarReporte(tipoReporte, filtros, cancelacion);
        if (resultado.Error is not null)
        {
            return resultado.Error;
        }

        var archivo = GeneradorReportesCsv.Generar(resultado.Reporte!.Tablas);
        MetricasDeNegocio.ReportesGenerados.Add(1, new KeyValuePair<string, object?>("tipo", tipoReporte),
            new KeyValuePair<string, object?>("formato", "csv"));
        return File(archivo, "text/csv; charset=utf-8", "reporte-" + tipoReporte + ".csv");
    }

    private async Task<(ReporteArmado? Reporte, IActionResult? Error)> ArmarReporte(string tipoReporte, FiltrosIndicadoresDto filtros, CancellationToken cancelacion)
    {
        if (RangoInvertido(filtros))
        {
            return (null, RangoDeFechasInvalido());
        }

        if (tipoReporte is not ("ordenes-trabajo" or "activos-intervenidos" or "repuestos-utilizados"
            or "actividad-tecnicos" or "indicadores"))
        {
            return (null, this.RespuestaDeError(400, "TIPO_REPORTE_INVALIDO", "El tipo de reporte no es válido."));
        }

        var filtro = AFiltroDeOrdenes(filtros);
        var tipoValido = TipoValido(filtros);
        var ordenes = tipoValido ? await _ordenTrabajoDao.ListarResumenesAsync(filtro, cancelacion) : [];
        var criterios = await ArmarCriterios(filtros, cancelacion);
        var reglas = await _ordenTrabajoDao.ObtenerReglasAsync(cancelacion);

        return tipoReporte switch
        {
            "ordenes-trabajo" => (new ReporteArmado("Reporte de órdenes de trabajo", criterios,
                [ArmarTablaOrdenes(ordenes)]), null),
            "activos-intervenidos" => (new ReporteArmado("Reporte de activos intervenidos", criterios,
                [ArmarTablaActivos(ordenes)]), null),
            "repuestos-utilizados" => (new ReporteArmado("Reporte de repuestos utilizados", criterios,
                [ArmarTablaRepuestos(tipoValido ? await _ordenTrabajoDao.ListarRepuestosUsadosAsync(filtro, cancelacion) : [])]), null),
            "actividad-tecnicos" => (new ReporteArmado("Reporte de actividad por técnico", criterios,
                [ArmarTablaTecnicos(ordenes, reglas)]), null),
            _ => (new ReporteArmado("Reporte de indicadores operativos", criterios,
            [
                ArmarTablaResumen(ordenes),
                ArmarTablaGrupos("Órdenes por estado", "Estado", Agrupar(ordenes, o => o.Estado)),
                ArmarTablaGrupos("Órdenes por prioridad", "Prioridad",
                    Agrupar(ordenes, o => OrdenTrabajoMapeador.APrioridadTexto(o.Prioridad))),
                ArmarTablaGrupos("Órdenes por tipo de mantenimiento", "Tipo", Agrupar(ordenes, TextoDelTipo)),
                ArmarTablaTecnicos(ordenes, reglas),
                ArmarTablaActivos(ordenes)
            ]), null),
        };
    }

    private sealed record ReporteArmado(string Titulo, List<string> Criterios, List<TablaReporte> Tablas);

    private static bool RangoInvertido(FiltrosIndicadoresDto filtros) =>
        filtros.FechaDesde is not null && filtros.FechaHasta is not null && filtros.FechaDesde > filtros.FechaHasta;

    private ObjectResult RangoDeFechasInvalido() => this.RespuestaDeError(400, "RANGO_FECHAS_INVALIDO", "La fecha desde no puede ser posterior a la fecha hasta.");

    private static bool TipoValido(FiltrosIndicadoresDto filtros) =>
        filtros.Tipo is null or "" or "preventivo" or "correctivo";

    private static FiltroOrdenesTrabajo AFiltroDeOrdenes(FiltrosIndicadoresDto filtros) => new(
        Buscar: null, filtros.ClienteId, filtros.ActivoId, filtros.TecnicoId, filtros.Estado, filtros.Prioridad,
        EsPreventivo: filtros.Tipo switch { "preventivo" => true, "correctivo" => false, _ => null },
        filtros.FechaDesde, filtros.FechaHasta, ConSugerencias: null, OrdenarPorCreacion: false, Descendente: false);

    private static string TextoDelTipo(OrdenResumen orden) => orden.EsPreventivo ? "preventivo" : "correctivo";

    private static bool EstaTerminada(ReglasDeEstadoOrden reglas, string estado) =>
        reglas.FaseDe(estado) is { } fase && FasesDeEstado.Terminadas.Contains(fase);

    /// <summary>El trabajo ya se hizo: está por validar o cerrada.</summary>
    private static bool TieneElTrabajoHecho(ReglasDeEstadoOrden reglas, string estado) =>
        reglas.FaseDe(estado) is FasesDeEstado.PorValidar or FasesDeEstado.Cerrada;

    private static List<CantidadPorGrupoDto> Agrupar(IEnumerable<OrdenResumen> ordenes, Func<OrdenResumen, string> clave) =>
        ordenes.GroupBy(clave)
            .Select(grupo => new CantidadPorGrupoDto { Nombre = grupo.Key, Cantidad = grupo.Count() })
            .ToList();

    private async Task<List<string>> ArmarCriterios(FiltrosIndicadoresDto filtros, CancellationToken cancelacion)
    {
        var criterios = new List<string>();

        if (filtros.FechaDesde is not null)
        {
            criterios.Add("Desde: " + filtros.FechaDesde.Value.ToString("dd/MM/yyyy"));
        }

        if (filtros.FechaHasta is not null)
        {
            criterios.Add("Hasta: " + filtros.FechaHasta.Value.ToString("dd/MM/yyyy"));
        }

        if (filtros.ClienteId is { } clienteId)
        {
            criterios.Add("Cliente: " + (await _ordenTrabajoDao.ObtenerNombreDeClienteAsync(clienteId, cancelacion) ?? "N° " + clienteId));
        }

        if (filtros.ActivoId is { } activoId)
        {
            criterios.Add("Activo: " + (await _ordenTrabajoDao.ObtenerNombreDeActivoAsync(activoId, cancelacion) ?? "N° " + activoId));
        }

        if (filtros.TecnicoId is { } tecnicoId)
        {
            criterios.Add("Técnico: " + (await _ordenTrabajoDao.ObtenerNombreDeTecnicoAsync(tecnicoId, cancelacion) ?? "N° " + tecnicoId));
        }

        if (!string.IsNullOrEmpty(filtros.Estado))
        {
            criterios.Add("Estado: " + filtros.Estado);
        }

        if (!string.IsNullOrEmpty(filtros.Prioridad))
        {
            criterios.Add("Prioridad: " + filtros.Prioridad);
        }

        if (!string.IsNullOrEmpty(filtros.Tipo))
        {
            criterios.Add("Tipo de mantenimiento: " + filtros.Tipo);
        }

        if (criterios.Count == 0)
        {
            criterios.Add("Sin filtros: incluye todas las órdenes de trabajo.");
        }

        return criterios;
    }

    internal static TablaReporte ArmarTablaOrdenes(List<OrdenResumen> ordenes)
    {
        var recortado = ordenes.Count > MaximoDeOrdenesListadas;
        var tabla = new TablaReporte
        {
            Titulo = recortado
                ? $"Órdenes de trabajo (primeras {MaximoDeOrdenesListadas} de {ordenes.Count}; filtrá por fechas, cliente o técnico para ver el resto)"
                : "Órdenes de trabajo (" + ordenes.Count + ")",
            Columnas = new List<string> { "N°", "Creada", "Cliente", "Activo", "Técnico", "Tipo", "Prioridad", "Estado" }
        };

        foreach (var orden in ordenes.Take(MaximoDeOrdenesListadas))
        {
            tabla.Filas.Add(new List<string>
            {
                orden.Id.ToString(),
                HoraUruguay.Convertir(orden.CreadoEn).ToString("dd/MM/yyyy"),
                orden.Cliente,
                orden.Activo,
                orden.Tecnico ?? "Sin asignar",
                TextoDelTipo(orden),
                OrdenTrabajoMapeador.APrioridadTexto(orden.Prioridad),
                orden.Estado
            });
        }

        return tabla;
    }

    private static TablaReporte ArmarTablaActivos(List<OrdenResumen> ordenes)
    {
        var tabla = new TablaReporte
        {
            Titulo = "Activos intervenidos",
            Columnas = new List<string> { "Activo", "Cliente", "Órdenes de trabajo" }
        };

        foreach (var activo in ordenes.GroupBy(o => o.Activo).OrderByDescending(grupo => grupo.Count()))
        {
            tabla.Filas.Add(new List<string> { activo.Key, activo.Last().Cliente, activo.Count().ToString() });
        }

        return tabla;
    }

    private static TablaReporte ArmarTablaRepuestos(List<RepuestoUsadoEnOrdenes> repuestos)
    {
        var tabla = new TablaReporte
        {
            Titulo = "Repuestos utilizados",
            Columnas = new List<string> { "Repuesto", "Órdenes de trabajo en las que se usó" }
        };

        foreach (var repuesto in repuestos)
        {
            tabla.Filas.Add(new List<string> { repuesto.Codigo + " - " + repuesto.Nombre, repuesto.Cantidad.ToString() });
        }

        return tabla;
    }

    private static TablaReporte ArmarTablaTecnicos(List<OrdenResumen> ordenes, ReglasDeEstadoOrden reglas)
    {
        var tabla = new TablaReporte
        {
            Titulo = "Actividad por técnico",
            Columnas = new List<string> { "Técnico", "Órdenes asignadas", "Finalizadas o cerradas", "Pendientes" }
        };

        foreach (var tecnico in ordenes.Where(o => o.Tecnico is not null).GroupBy(o => o.Tecnico!))
        {
            var asignadas = tecnico.Count();
            var terminadas = tecnico.Count(o => TieneElTrabajoHecho(reglas, o.Estado));
            tabla.Filas.Add(new List<string>
            {
                tecnico.Key, asignadas.ToString(), terminadas.ToString(), (asignadas - terminadas).ToString()
            });
        }

        return tabla;
    }

    private static TablaReporte ArmarTablaResumen(List<OrdenResumen> ordenes)
    {
        var horasHastaInicio = ordenes.Where(o => o.FechaInicio is not null)
            .Select(o => (o.FechaInicio!.Value - o.CreadoEn).TotalHours).ToList();
        var horasHastaCierre = ordenes.Where(o => o.FechaCierre is not null)
            .Select(o => (o.FechaCierre!.Value - o.CreadoEn).TotalHours).ToList();

        var tabla = new TablaReporte
        {
            Titulo = "Resumen",
            Columnas = new List<string> { "Indicador", "Valor" }
        };
        tabla.Filas.Add(new List<string> { "Órdenes de trabajo registradas", ordenes.Count.ToString() });
        tabla.Filas.Add(new List<string> { "Tiempo promedio hasta el inicio", FormatearHoras(CalcularPromedio(horasHastaInicio)) });
        tabla.Filas.Add(new List<string> { "Tiempo promedio hasta el cierre", FormatearHoras(CalcularPromedio(horasHastaCierre)) });
        return tabla;
    }

    private static TablaReporte ArmarTablaGrupos(string titulo, string columna, List<CantidadPorGrupoDto> grupos)
    {
        var tabla = new TablaReporte
        {
            Titulo = titulo,
            Columnas = new List<string> { columna, "Cantidad" }
        };

        foreach (var grupo in grupos)
        {
            tabla.Filas.Add(new List<string> { grupo.Nombre, grupo.Cantidad.ToString() });
        }

        return tabla;
    }

    private static string FormatearHoras(double? horas) => horas is null ? "Sin datos" : horas + " h";

    private static double? CalcularPromedio(List<double> valores) =>
        valores.Count == 0 ? null : Math.Round(valores.Average(), 1);
}
