using Api.DTOs.Reportes;
using Api.Infraestructura;
using Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

/// <summary>Métricas del dashboard analítico: tendencias, tiempos, reincidencias y alertas de maquinaria.</summary>
[ApiController]
[Route("api/v1/reportes")]
public class AnaliticaController : ControllerBase
{
    private const int MaximoDeReincidencias = 20;
    private const int MaximoDeRepuestosUsados = 10;
    private const int MaximoDeMotivos = 5;
    private const int MaximoDeActivos = 10;

    private readonly AnaliticaDAO _analiticaDao;
    private readonly AnaliticaAvanzadaDAO _avanzadaDao;
    private readonly OpcionesDeAlertasDeMaquinaria _opciones;
    private readonly CacheDeAnalitica _cache;

    public AnaliticaController(AnaliticaDAO analiticaDao, AnaliticaAvanzadaDAO avanzadaDao, IOptions<OpcionesDeAlertasDeMaquinaria> opciones, CacheDeAnalitica cache)
    {
        _analiticaDao = analiticaDao;
        _avanzadaDao = avanzadaDao;
        _cache = cache;
        _opciones = opciones.Value;
    }

    /// <summary>Tendencia de órdenes por período.</summary>
    [HttpGet("tendencias")]
    [Authorize(Policy = Politicas.ReporteConsultar)]
    [ProducesResponseType<TendenciasDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TendenciasDto>> ObtenerTendencias([FromQuery] FiltrosAnaliticaDto filtros, CancellationToken cancelacion)
    {
        if (RangoInvalido(filtros) is { } error)
        {
            return error;
        }

        var agrupacion = (filtros.Agrupacion ?? "semana").ToLowerInvariant();
        if (agrupacion is not ("semana" or "mes"))
        {
            return this.RespuestaDeError(400, "AGRUPACION_INVALIDA", "La agrupación debe ser semana o mes.");
        }

        var filtro = ADominio(filtros);
        return Ok(await _cache.ObtenerAsync($"tendencias|{agrupacion}|{filtro}",
            token => CalcularTendenciasAsync(agrupacion, filtro, token), cancelacion));
    }

    private async Task<TendenciasDto> CalcularTendenciasAsync(string agrupacion, FiltroAnalitica filtro, CancellationToken cancelacion)
    {
        var unidad = agrupacion == "mes" ? "month" : "week";
        var filas = await _analiticaDao.ObtenerTendenciasAsync(unidad, filtro, cancelacion);
        var cierres = (await _avanzadaDao.ObtenerCierresPorPeriodoAsync(unidad, filtro, cancelacion))
            .ToDictionary(cierre => DateOnly.FromDateTime(cierre.Periodo), cierre => cierre.Cerradas);
        var puntos = new SortedDictionary<DateOnly, PuntoTendenciaDto>();
        foreach (var fila in filas)
        {
            var periodo = DateOnly.FromDateTime(fila.Periodo);
            puntos[periodo] = new PuntoTendenciaDto
            {
                Periodo = periodo.ToString("yyyy-MM-dd"),
                Preventivas = fila.Preventivas,
                Correctivas = fila.Correctivas,
                Total = fila.Preventivas + fila.Correctivas,
                Cerradas = cierres.GetValueOrDefault(periodo),
            };
        }

        foreach (var (periodo, cerradas) in cierres.Where(cierre => !puntos.ContainsKey(cierre.Key)))
        {
            puntos[periodo] = new PuntoTendenciaDto { Periodo = periodo.ToString("yyyy-MM-dd"), Cerradas = cerradas };
        }

        var lista = puntos.Values.ToList();

        int? totalAnterior = null;
        if (filtro.Desde is { } desde && filtro.Hasta is { } hasta)
        {
            var dias = hasta.DayNumber - desde.DayNumber + 1;
            totalAnterior = await _analiticaDao.ContarOrdenesAsync(
                filtro with { Desde = desde.AddDays(-dias), Hasta = desde.AddDays(-1) }, cancelacion);
        }

        return new TendenciasDto
        {
            Agrupacion = agrupacion,
            Puntos = lista,
            TotalPeriodo = lista.Sum(punto => punto.Total),
            TotalPeriodoAnterior = totalAnterior,
        };
    }

    /// <summary>Desempeño operativo: órdenes, tiempos, pausas, devoluciones y maquinaria (sin evaluar personas).</summary>
    [HttpGet("desempeno")]
    [Authorize(Policy = Politicas.ReporteConsultar)]
    [ProducesResponseType<DesempenoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DesempenoDto>> ObtenerDesempeno([FromQuery] FiltrosAnaliticaDto filtros, CancellationToken cancelacion)
    {
        if (RangoInvalido(filtros) is { } error)
        {
            return error;
        }

        var filtro = ADominio(filtros);
        return Ok(await _cache.ObtenerAsync($"desempeno|{filtro}", token => CalcularDesempenoAsync(filtro, token), cancelacion));
    }

    private async Task<DesempenoDto> CalcularDesempenoAsync(FiltroAnalitica filtro, CancellationToken cancelacion)
    {
        var resumen = await _analiticaDao.ObtenerResumenDeOrdenesAsync(filtro, cancelacion);
        var (respuesta, ejecucion, revision) = await _analiticaDao.ObtenerTiemposDeProcesoAsync(filtro, cancelacion);
        var pausas = await _analiticaDao.ObtenerPausasAsync(filtro, cancelacion);
        var devoluciones = await _analiticaDao.ObtenerDevolucionesAsync(filtro, cancelacion);
        var reasignaciones = await _analiticaDao.ObtenerReasignacionesAsync(filtro, cancelacion);
        var pausasPorMotivo = await _avanzadaDao.ObtenerPausasPorMotivoAsync(filtro, cancelacion);
        var totalParada = await _analiticaDao.ObtenerTotalHorasParadaAsync(filtro, cancelacion);
        var porTipo = await _analiticaDao.ObtenerMaquinariaPorTipoAsync(filtro, cancelacion);
        var porActivo = await _analiticaDao.ObtenerMaquinariaPorActivoAsync(MaximoDeActivos, filtro, cancelacion);
        var porModelo = await _analiticaDao.ObtenerMaquinariaPorModeloAsync(filtro, cancelacion);
        var estimacion = await _avanzadaDao.ObtenerEstimacionAsync(filtro, cancelacion);
        // La tarjeta y la comparación comparten los últimos 90 días cuando no se eligen fechas.
        var hastaPlanificacion = filtro.Hasta ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var desdePlanificacion = filtro.Desde ?? hastaPlanificacion.AddDays(-89);
        var planificacion = await _avanzadaDao.ObtenerPlanificacionAsync(
            filtro with { Desde = desdePlanificacion, Hasta = hastaPlanificacion }, cancelacion);

        return new DesempenoDto
        {
            Ordenes = new ResumenDeOrdenesDto
            {
                Creadas = resumen.Creadas,
                Cerradas = resumen.Cerradas,
                Eliminadas = resumen.Eliminadas,
                Abiertas = resumen.Abiertas,
                Preventivas = resumen.Preventivas,
                Correctivas = resumen.Correctivas,
                PorcentajePreventivas = Porcentaje(resumen.Preventivas, resumen.Creadas),
            },
            Tiempos = new TiemposDeProcesoDto
            {
                Respuesta = ADuracion(respuesta),
                Ejecucion = ADuracion(ejecucion),
                Revision = ADuracion(revision),
            },
            Flujo = new FlujoDeProcesoDto
            {
                Pausas = new PausasDto
                {
                    Cantidad = pausas.Cantidad,
                    OrdenesConPausa = pausas.Ordenes,
                    DuracionPromedioHoras = Redondear(pausas.PromedioHoras),
                    Motivos = AMotivos(await _analiticaDao.ObtenerMotivosDePausaAsync(MaximoDeMotivos, filtro, cancelacion)),
                    PorMotivo = pausasPorMotivo.Select(fila => new PausaPorMotivoDto
                    {
                        Codigo = fila.Codigo,
                        Motivo = fila.Motivo,
                        Cantidad = fila.Cantidad,
                        Horas = Redondear(fila.Horas)!.Value,
                        HorasPromedio = fila.Cantidad == 0 ? null : Redondear(fila.Horas / fila.Cantidad),
                    }).ToList(),
                },
                Devoluciones = new DevolucionesDto
                {
                    Cantidad = devoluciones.Cantidad,
                    OrdenesDevueltas = devoluciones.Ordenes,
                    PorcentajeDeOrdenes = Porcentaje(devoluciones.Ordenes, resumen.Creadas),
                    Motivos = AMotivos(await _analiticaDao.ObtenerMotivosDeDevolucionAsync(MaximoDeMotivos, filtro, cancelacion)),
                },
                Reasignaciones = new ReasignacionesDto { Cantidad = reasignaciones.Cantidad, OrdenesReasignadas = reasignaciones.Ordenes },
            },
            Maquinaria = new MaquinariaDto
            {
                PorTipo = AMaquinariaConPareto(porTipo, totalParada),
                Activos = AMaquinariaConPareto(porActivo, totalParada),
                PorModelo = AMaquinariaConPareto(porModelo, totalParada),
            },
            Estimacion = new EstimacionDto
            {
                Ordenes = estimacion.Ordenes,
                HorasEstimadas = Redondear(estimacion.HorasEstimadas),
                HorasReales = Redondear(estimacion.HorasReales),
                DesviacionPromedioPorcentaje = Redondear(estimacion.DesviacionPromedioPorcentaje),
            },
            Planificacion = new PlanificacionDto
            {
                Periodo = new RangoDeFechasDto
                {
                    Desde = desdePlanificacion.ToString("yyyy-MM-dd"),
                    Hasta = hastaPlanificacion.ToString("yyyy-MM-dd"),
                },
                Resumen = AResumenDeHoras(planificacion),
                PorMes = planificacion.GroupBy(fila => fila.Periodo).OrderBy(grupo => grupo.Key)
                    .Select(grupo => new PlanificacionPorMesDto
                    {
                        Periodo = grupo.Key.ToString("yyyy-MM-dd"),
                        Resumen = AResumenDeHoras(grupo),
                    }).ToList(),
                PorTipo = planificacion.GroupBy(fila => new { fila.TipoMaquinaId, fila.TipoMaquina })
                    .OrderBy(grupo => grupo.Key.TipoMaquinaId)
                    .Select(grupo => new PlanificacionPorTipoDto
                    {
                        Id = grupo.Key.TipoMaquinaId,
                        Nombre = grupo.Key.TipoMaquina,
                        Resumen = AResumenDeHoras(grupo),
                    }).ToList(),
            },
        };
    }

    private static HorasMantenimientoDto AResumenDeHoras(IEnumerable<PlanificacionFila> filas)
    {
        var lista = filas.ToList();
        var preventivas = lista.Sum(fila => fila.HorasPreventivas);
        var correctivas = lista.Sum(fila => fila.HorasCorrectivas);
        var total = preventivas + correctivas;
        return new HorasMantenimientoDto
        {
            OrdenesCerradas = lista.Sum(fila => fila.OrdenesCerradas),
            HorasPreventivas = Redondear(preventivas)!.Value,
            HorasCorrectivas = Redondear(correctivas)!.Value,
            HorasTotales = Redondear(total)!.Value,
            PorcentajePlanificado = total == 0 ? null : Redondear(preventivas * 100.0 / total),
        };
    }

    private static DuracionDto ADuracion(DuracionFila fila) => new()
    {
        Cantidad = fila.Cantidad,
        PromedioHoras = Redondear(fila.Promedio),
        MedianaHoras = Redondear(fila.Mediana),
        Percentil90Horas = Redondear(fila.Percentil90),
    };

    private static List<MotivoDto> AMotivos(List<MotivoFila> filas) =>
        filas.Select(fila => new MotivoDto { Texto = fila.Texto == "" ? "Sin motivo indicado" : fila.Texto, Cantidad = fila.Cantidad }).ToList();

    /// <summary>Convierte las filas (ya ordenadas de mayor a menor parada) y agrega el porcentaje de cada una y el acumulado.</summary>
    private static List<MaquinariaItemDto> AMaquinariaConPareto(List<MaquinariaFila> filas, double totalParada)
    {
        var acumulado = 0.0;
        var elementos = new List<MaquinariaItemDto>(filas.Count);
        foreach (var fila in filas)
        {
            acumulado += fila.HorasParada;
            elementos.Add(AMaquinaria(fila, totalParada, acumulado));
        }

        return elementos;
    }

    private static MaquinariaItemDto AMaquinaria(MaquinariaFila fila, double totalParada, double acumulado) => new()
    {
        PorcentajeDeLaParada = totalParada > 0 ? Redondear(fila.HorasParada * 100.0 / totalParada) : null,
        PorcentajeAcumulado = totalParada > 0 ? Redondear(Math.Min(100.0, acumulado * 100.0 / totalParada)) : null,
        CorrectivasCerradas = fila.CorrectivasCerradas,
        TiempoMedioReparacionHoras = fila.CorrectivasCerradas == 0 ? null : Redondear(fila.HorasParada / fila.CorrectivasCerradas),
        ActivosEnServicio = fila.ActivosTotales > 0 ? fila.ActivosTotales : null,
        CostoParada = fila.CostoHoraParada is { } costo ? Math.Round((decimal)(fila.HorasParada * costo), 2) : null,
        Id = fila.Id,
        Nombre = fila.Nombre,
        Cliente = fila.Cliente,
        Ordenes = fila.Ordenes,
        Correctivas = fila.Correctivas,
        Preventivas = fila.Preventivas,
        HorasPromedioEjecucion = Redondear(fila.HorasPromedioEjecucion),
        HorasParada = Redondear(fila.HorasParada)!.Value,
    };

    private static double? Porcentaje(int parte, int total) => total == 0 ? null : Redondear(parte * 100.0 / total);

    private static CumplimientoGrupoDto ACumplimiento(long? id, string nombre, int evaluadas, int enPlazo) => new()
    {
        Id = id,
        Nombre = nombre,
        Evaluadas = evaluadas,
        EnPlazo = enPlazo,
        PorcentajeEnPlazo = Porcentaje(enPlazo, evaluadas),
    };

    /// <summary>Tiempos de respuesta, ejecución y cierre de las órdenes.</summary>
    [HttpGet("tiempos")]
    [Authorize(Policy = Politicas.ReporteConsultar)]
    [ProducesResponseType<TiemposDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TiemposDto>> ObtenerTiempos([FromQuery] FiltrosAnaliticaDto filtros, CancellationToken cancelacion)
    {
        if (RangoInvalido(filtros) is { } error)
        {
            return error;
        }

        var filtro = ADominio(filtros);
        return Ok(await _cache.ObtenerAsync($"tiempos|{filtro}", token => CalcularTiemposAsync(filtro, token), cancelacion));
    }

    private async Task<TiemposDto> CalcularTiemposAsync(FiltroAnalitica filtro, CancellationToken cancelacion)
    {
        var duracion = await _analiticaDao.ObtenerDuracionDeOrdenesCerradasAsync(filtro, cancelacion);
        var etapas = await _analiticaDao.ObtenerTiempoPorEtapaAsync(filtro, cancelacion);
        var cumplimiento = await _analiticaDao.ObtenerCumplimientoDeFechasAsync(filtro, cancelacion);
        var porTipo = await _avanzadaDao.ObtenerCumplimientoPorTipoAsync(filtro, cancelacion);

        return new TiemposDto
        {
            CumplimientoPorTipoDeOrden = porTipo
                .GroupBy(fila => fila.EsPreventiva)
                .OrderByDescending(grupo => grupo.Key)
                .Select(grupo => ACumplimiento(null, grupo.Key ? "preventivo" : "correctivo", grupo.Sum(fila => fila.Evaluadas), grupo.Sum(fila => fila.EnPlazo)))
                .ToList(),
            CumplimientoPreventivoPorTipoDeMaquina = porTipo
                .Where(fila => fila.EsPreventiva)
                .Select(fila => ACumplimiento(fila.TipoMaquinaId, fila.TipoMaquina, fila.Evaluadas, fila.EnPlazo))
                .ToList(),
            Duracion = new DuracionDto
            {
                Cantidad = duracion.Cantidad,
                PromedioHoras = Redondear(duracion.Promedio),
                MedianaHoras = Redondear(duracion.Mediana),
                Percentil90Horas = Redondear(duracion.Percentil90),
            },
            Etapas = etapas.Select(etapa => new EtapaDto
            {
                Estado = etapa.Estado,
                Cantidad = etapa.Cantidad,
                PromedioHoras = Redondear(etapa.PromedioHoras)!.Value,
            }).ToList(),
            Cumplimiento = new CumplimientoDto
            {
                Evaluadas = cumplimiento.Evaluadas,
                EnPlazo = cumplimiento.EnPlazo,
                PorcentajeEnPlazo = cumplimiento.Evaluadas == 0
                    ? null
                    : Redondear(cumplimiento.EnPlazo * 100.0 / cumplimiento.Evaluadas),
                AtrasoPromedioDias = Redondear(cumplimiento.AtrasoPromedioDias),
            },
        };
    }

    /// <summary>Repuestos más utilizados en las órdenes.</summary>
    [HttpGet("repuestos-usados")]
    [Authorize(Policy = Politicas.ReporteConsultar)]
    [ProducesResponseType<RepuestosUsadosDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RepuestosUsadosDto>> ObtenerRepuestosUsados([FromQuery] FiltrosAnaliticaDto filtros, CancellationToken cancelacion)
    {
        if (RangoInvalido(filtros) is { } error)
        {
            return error;
        }

        var filtro = ADominio(filtros);
        return Ok(await _cache.ObtenerAsync($"repuestos|{filtro}", token => CalcularRepuestosUsadosAsync(filtro, token), cancelacion));
    }

    private async Task<RepuestosUsadosDto> CalcularRepuestosUsadosAsync(FiltroAnalitica filtro, CancellationToken cancelacion)
    {
        var filas = await _analiticaDao.ObtenerRepuestosMasUsadosAsync(MaximoDeRepuestosUsados, filtro, cancelacion);
        return new RepuestosUsadosDto
        {
            Elementos = filas.Select(fila => new RepuestoUsadoDto
            {
                RepuestoId = fila.RepuestoId,
                Codigo = fila.Codigo,
                Nombre = fila.Nombre,
                Ordenes = fila.Ordenes,
            }).ToList(),
        };
    }

    /// <summary>Activos con fallas repetidas en un período.</summary>
    [HttpGet("reincidencias")]
    [Authorize(Policy = Politicas.ReporteConsultar)]
    [ProducesResponseType<ReincidenciasDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReincidenciasDto>> ObtenerReincidencias([FromQuery] FiltrosAnaliticaDto filtros, CancellationToken cancelacion)
    {
        if (RangoInvalido(filtros) is { } error)
        {
            return error;
        }

        var dias = filtros.Dias ?? _opciones.ReincidenciaDias;
        var minimo = filtros.Minimo ?? 1;
        if (dias is < 1 or > 3650 || minimo is < 1 or > 100)
        {
            return this.RespuestaDeError(400, "PARAMETRO_INVALIDO", "Los días deben estar entre 1 y 3650 y el mínimo de reincidencias entre 1 y 100.");
        }

        var filtro = ADominio(filtros);
        return Ok(await _cache.ObtenerAsync($"reincidencias|{dias}|{minimo}|{filtro}",
            token => CalcularReincidenciasAsync(dias, minimo, filtro, token), cancelacion));
    }

    private async Task<ReincidenciasDto> CalcularReincidenciasAsync(int dias, int minimo, FiltroAnalitica filtro, CancellationToken cancelacion)
    {
        var filas = await _analiticaDao.ObtenerReincidenciasAsync(dias, minimo, MaximoDeReincidencias, filtro, cancelacion);
        return new ReincidenciasDto
        {
            Dias = dias,
            Minimo = minimo,
            Elementos = filas.Select(fila => new ReincidenciaDto
            {
                ActivoId = fila.ActivoId,
                NombreActivo = fila.NombreActivo,
                Cliente = fila.Cliente,
                Correctivas = fila.Correctivas,
                Reincidencias = fila.Reincidencias,
                DiasPromedioEntreIntervenciones = Redondear(fila.DiasPromedioEntreIntervenciones),
                UltimaIntervencion = DateTime.SpecifyKind(fila.UltimaIntervencion, DateTimeKind.Utc),
            }).ToList(),
        };
    }

    /// <summary>Órdenes abiertas que superan los umbrales de alerta.</summary>
    [HttpGet("alertas-maquinaria")]
    [Authorize(Policy = Politicas.ReporteConsultar)]
    [ProducesResponseType<AlertasMaquinariaDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AlertasMaquinariaDto>> ObtenerAlertasMaquinaria([FromQuery] FiltrosAnaliticaDto filtros, CancellationToken cancelacion)
    {
        var filtro = new FiltroAnalitica(null, null, filtros.ClienteId, filtros.ActivoId);
        return Ok(await _cache.ObtenerAsync($"alertas|{filtro}", token => CalcularAlertasAsync(filtro, token), cancelacion));
    }

    private async Task<AlertasMaquinariaDto> CalcularAlertasAsync(FiltroAnalitica filtro, CancellationToken cancelacion)
    {
        var alertas = new List<AlertaMaquinariaDto>();
        foreach (var orden in await _analiticaDao.ListarOrdenesAbiertasAsync(filtro, cancelacion))
        {
            var prioridad = orden.Prioridad >= 5 ? "alta" : orden.Prioridad >= 3 ? "media" : "baja";

            // La alerta del estado la define la base (`estado_ot.codigo_alerta`); aquí solo se conoce su umbral.
            if (orden.CodigoAlerta is { } codigoAlerta && UmbralDeLaAlerta(codigoAlerta) is { } umbralDelEstado)
            {
                AgregarSiSuperaElUmbral(alertas, orden, prioridad, codigoAlerta, orden.HorasEnEstado, umbralDelEstado);
            }

            var umbralDeDetencion = prioridad switch
            {
                "alta" => _opciones.MaquinaDetenidaAltaHoras,
                "media" => _opciones.MaquinaDetenidaMediaHoras,
                _ => _opciones.MaquinaDetenidaBajaHoras,
            };
            AgregarSiSuperaElUmbral(alertas, orden, prioridad, "maquina_detenida", orden.HorasDesdeCreacion, umbralDeDetencion);
        }

        var ordenadas = alertas
            .OrderByDescending(alerta => alerta.Gravedad == "critica")
            .ThenByDescending(alerta => alerta.HorasTranscurridas)
            .ToList();
        return new AlertasMaquinariaDto
        {
            Total = ordenadas.Count,
            Criticas = ordenadas.Count(alerta => alerta.Gravedad == "critica"),
            Alertas = ordenadas,
        };
    }

    private int? UmbralDeLaAlerta(string codigo) => codigo switch
    {
        "sin_asignar" => _opciones.SinAsignarHoras,
        "demorada_para_iniciar" => _opciones.AsignadaSinIniciarHoras,
        "pausada_demasiado" => _opciones.PausadaHoras,
        _ => null,
    };

    private static void AgregarSiSuperaElUmbral(List<AlertaMaquinariaDto> alertas, OrdenAbiertaFila orden,
        string prioridad, string tipo, double horas, int umbral)
    {
        if (horas < umbral)
        {
            return;
        }

        alertas.Add(new AlertaMaquinariaDto
        {
            Tipo = tipo,
            Gravedad = horas >= umbral * 2.0 ? "critica" : "alta",
            OrdenId = orden.OrdenId,
            ActivoId = orden.ActivoId,
            NombreActivo = orden.NombreActivo,
            Cliente = orden.Cliente,
            Estado = orden.Estado,
            Prioridad = prioridad,
            HorasTranscurridas = Redondear(horas)!.Value,
            UmbralHoras = umbral,
        });
    }

    private ActionResult? RangoInvalido(FiltrosAnaliticaDto filtros)
    {
        if (filtros.FechaDesde is not null && filtros.FechaHasta is not null && filtros.FechaDesde > filtros.FechaHasta)
        {
            return this.RespuestaDeError(400, "RANGO_FECHAS_INVALIDO", "La fecha desde no puede ser posterior a la fecha hasta.");
        }

        return null;
    }

    private static FiltroAnalitica ADominio(FiltrosAnaliticaDto filtros)
    {
        return new FiltroAnalitica(filtros.FechaDesde, filtros.FechaHasta, filtros.ClienteId, filtros.ActivoId);
    }

    private static double? Redondear(double? valor)
    {
        return valor is null ? null : Math.Round(valor.Value, 2);
    }
}
