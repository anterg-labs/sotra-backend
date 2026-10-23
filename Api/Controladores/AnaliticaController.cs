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

    private readonly AnaliticaDAO _analiticaDao;
    private readonly OpcionesDeAlertasDeMaquinaria _opciones;
    private readonly CacheDeAnalitica _cache;

    public AnaliticaController(AnaliticaDAO analiticaDao, IOptions<OpcionesDeAlertasDeMaquinaria> opciones, CacheDeAnalitica cache)
    {
        _analiticaDao = analiticaDao;
        _cache = cache;
        _opciones = opciones.Value;
    }

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
        var filas = await _analiticaDao.ObtenerTendenciasAsync(agrupacion == "mes" ? "month" : "week", filtro, cancelacion);
        var puntos = filas.Select(fila => new PuntoTendenciaDto
        {
            Periodo = DateOnly.FromDateTime(fila.Periodo).ToString("yyyy-MM-dd"),
            Preventivas = fila.Preventivas,
            Correctivas = fila.Correctivas,
            Total = fila.Preventivas + fila.Correctivas,
        }).ToList();

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
            Puntos = puntos,
            TotalPeriodo = puntos.Sum(punto => punto.Total),
            TotalPeriodoAnterior = totalAnterior,
        };
    }

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

        return new TiemposDto
        {
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

    [HttpGet("alertas-maquinaria")]
    [Authorize(Policy = Politicas.ReporteConsultar)]
    [ProducesResponseType<AlertasMaquinariaDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AlertasMaquinariaDto>> ObtenerAlertasMaquinaria([FromQuery] FiltrosAnaliticaDto filtros, CancellationToken cancelacion)
    {
        var filtro = new FiltroAnalitica(null, null, filtros.ClienteId, filtros.ActivoId, filtros.TecnicoId);
        return Ok(await _cache.ObtenerAsync($"alertas|{filtro}", token => CalcularAlertasAsync(filtro, token), cancelacion));
    }

    private async Task<AlertasMaquinariaDto> CalcularAlertasAsync(FiltroAnalitica filtro, CancellationToken cancelacion)
    {
        var alertas = new List<AlertaMaquinariaDto>();
        foreach (var orden in await _analiticaDao.ListarOrdenesAbiertasAsync(filtro, cancelacion))
        {
            var prioridad = orden.Prioridad >= 5 ? "alta" : orden.Prioridad >= 3 ? "media" : "baja";

            switch (orden.Estado)
            {
                case "creada":
                    AgregarSiSuperaElUmbral(alertas, orden, prioridad, "sin_asignar", orden.HorasEnEstado, _opciones.SinAsignarHoras);
                    break;
                case "asignada":
                    AgregarSiSuperaElUmbral(alertas, orden, prioridad, "demorada_para_iniciar", orden.HorasEnEstado, _opciones.AsignadaSinIniciarHoras);
                    break;
                case "pausada":
                    AgregarSiSuperaElUmbral(alertas, orden, prioridad, "pausada_demasiado", orden.HorasEnEstado, _opciones.PausadaHoras);
                    break;
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
        return new FiltroAnalitica(filtros.FechaDesde, filtros.FechaHasta, filtros.ClienteId, filtros.ActivoId, filtros.TecnicoId);
    }

    private static double? Redondear(double? valor)
    {
        return valor is null ? null : Math.Round(valor.Value, 2);
    }
}
