using Api.DTOs.Reportes;
using Api.Infraestructura;
using Api.Seguridad;
using Api.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

/// <summary>
/// Análisis avanzado de maquinaria y proceso: comparativa de períodos, envejecimiento de órdenes,
/// intervalos entre correctivas y avisos de ineficiencia. Nunca analiza ni compara personas.
/// </summary>
[ApiController]
[Route("api/v1/reportes")]
public class AnalisisController(AnaliticaAvanzadaDAO dao, ServicioDeAvisos avisos, SotraContext contexto, CacheDeAnalitica cache) : ControllerBase
{
    private const int MinimoDeIntervalos = 3;
    private const int MaximoDeActivosConDesgaste = 20;
    private const int MaximoDeMasAntiguas = 5;

    /// <summary>Indicadores clave del período frente al período anterior y al mismo período del año pasado.</summary>
    [HttpGet("analisis/comparativa")]
    [Authorize(Policy = Politicas.ReporteConsultar)]
    [ProducesResponseType<ComparativaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ComparativaDto>> ObtenerComparativa([FromQuery] FiltrosAnaliticaDto filtros, CancellationToken cancelacion)
    {
        if (RangoInvalido(filtros) is { } error)
        {
            return error;
        }

        var hasta = filtros.FechaHasta ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var desde = filtros.FechaDesde ?? hasta.AddDays(-89);
        var clave = $"comparativa|{desde}|{hasta}|{filtros.ClienteId}|{filtros.ActivoId}";
        return Ok(await cache.ObtenerAsync(clave,
            token => CalcularComparativaAsync(desde, hasta, filtros.ClienteId, filtros.ActivoId, token), cancelacion));
    }

    private async Task<ComparativaDto> CalcularComparativaAsync(DateOnly desde, DateOnly hasta, long? clienteId, long? activoId, CancellationToken cancelacion)
    {
        var dias = hasta.DayNumber - desde.DayNumber + 1;
        var anterior = (Desde: desde.AddDays(-dias), Hasta: desde.AddDays(-1));
        var anioAnterior = (Desde: desde.AddYears(-1), Hasta: hasta.AddYears(-1));
        var inicioDeSerie = new DateOnly(hasta.Year, hasta.Month, 1).AddMonths(-5);

        var actual = await dao.ObtenerKpisAsync(new FiltroAnalitica(desde, hasta, clienteId, activoId), cancelacion);
        var previo = await dao.ObtenerKpisAsync(new FiltroAnalitica(anterior.Desde, anterior.Hasta, clienteId, activoId), cancelacion);
        var delAnio = await dao.ObtenerKpisAsync(new FiltroAnalitica(anioAnterior.Desde, anioAnterior.Hasta, clienteId, activoId), cancelacion);
        var porMes = await dao.ObtenerKpisPorMesAsync(new FiltroAnalitica(inicioDeSerie, hasta, clienteId, activoId), cancelacion);

        var mesesDeLaSerie = Enumerable.Range(0, 6).Select(indice => inicioDeSerie.AddMonths(indice)).ToList();
        List<PuntoDeSerieDto> Serie(Func<KpisFila, double?> valor) => mesesDeLaSerie.Select(mes =>
        {
            var fila = porMes.FirstOrDefault(item => DateOnly.FromDateTime(item.Periodo) == mes);
            return new PuntoDeSerieDto
            {
                Periodo = mes.ToString("yyyy-MM-dd"),
                Valor = fila is null ? null : valor(new KpisFila
                {
                    Creadas = fila.Creadas,
                    Preventivas = fila.Preventivas,
                    Correctivas = fila.Correctivas,
                    PreventivasCerradas = fila.PreventivasCerradas,
                    PreventivasEnPlazo = fila.PreventivasEnPlazo,
                    CorrectivasCerradas = fila.CorrectivasCerradas,
                    HorasParada = fila.HorasParada,
                    HorasPreventivas = fila.HorasPreventivas,
                }),
            };
        }).ToList();

        MetricaComparadaDto Metrica(string codigo, string nombre, string unidad, bool? mejorSiSube,
            Func<KpisFila, double?> valor, Func<KpisFila, int> muestra)
        {
            var valorActual = valor(actual);
            var valorAnterior = valor(previo);
            return new MetricaComparadaDto
            {
                Codigo = codigo,
                Nombre = nombre,
                Unidad = unidad,
                Actual = Redondear(valorActual),
                Anterior = Redondear(valorAnterior),
                AnioAnterior = Redondear(valor(delAnio)),
                VariacionPorcentual = valorActual is { } a && valorAnterior is { } b && b != 0 ? Redondear((a - b) / Math.Abs(b) * 100.0) : null,
                MejorSiSube = mejorSiSube,
                Muestra = muestra(actual),
                Serie = Serie(valor),
            };
        }

        return new ComparativaDto
        {
            Periodo = new RangoDeFechasDto { Desde = desde.ToString("yyyy-MM-dd"), Hasta = hasta.ToString("yyyy-MM-dd") },
            PeriodoAnterior = new RangoDeFechasDto { Desde = anterior.Desde.ToString("yyyy-MM-dd"), Hasta = anterior.Hasta.ToString("yyyy-MM-dd") },
            PeriodoAnioAnterior = new RangoDeFechasDto { Desde = anioAnterior.Desde.ToString("yyyy-MM-dd"), Hasta = anioAnterior.Hasta.ToString("yyyy-MM-dd") },
            Metricas =
            [
                Metrica("preventivas_porcentaje", "Preventivas sobre el total", "porcentaje", true,
                    fila => fila.Creadas == 0 ? null : fila.Preventivas * 100.0 / fila.Creadas, fila => fila.Creadas),
                Metrica("planificado_horas_porcentaje", "Mantenimiento planificado en horas", "porcentaje", true,
                    fila => fila.HorasPreventivas + fila.HorasParada == 0 ? null
                        : fila.HorasPreventivas * 100.0 / (fila.HorasPreventivas + fila.HorasParada),
                    fila => fila.PreventivasCerradas + fila.CorrectivasCerradas),
                Metrica("cumplimiento_preventivo", "Preventivos cerrados en fecha", "porcentaje", true,
                    fila => fila.PreventivasCerradas == 0 ? null : fila.PreventivasEnPlazo * 100.0 / fila.PreventivasCerradas, fila => fila.PreventivasCerradas),
                Metrica("tiempo_reparacion_horas", "Tiempo medio de reparación", "horas", false,
                    fila => fila.CorrectivasCerradas == 0 ? null : fila.HorasParada / fila.CorrectivasCerradas, fila => fila.CorrectivasCerradas),
                Metrica("horas_parada", "Horas de parada", "horas", false,
                    fila => fila.CorrectivasCerradas == 0 ? null : fila.HorasParada, fila => fila.CorrectivasCerradas),
                Metrica("ordenes_creadas", "Órdenes creadas", "ordenes", null, fila => fila.Creadas, fila => fila.Creadas),
            ],
        };
    }

    /// <summary>Órdenes abiertas por antigüedad, backlog en semanas y las más antiguas.</summary>
    [HttpGet("envejecimiento")]
    [Authorize(Policy = Politicas.ReporteConsultar)]
    [ProducesResponseType<EnvejecimientoDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EnvejecimientoDto>> ObtenerEnvejecimiento([FromQuery] FiltrosAnaliticaDto filtros, CancellationToken cancelacion)
    {
        var filtro = new FiltroAnalitica(null, null, filtros.ClienteId, filtros.ActivoId);
        return Ok(await cache.ObtenerAsync($"envejecimiento|{filtro}", token => CalcularEnvejecimientoAsync(filtro, token), cancelacion));
    }

    private async Task<EnvejecimientoDto> CalcularEnvejecimientoAsync(FiltroAnalitica filtro, CancellationToken cancelacion)
    {
        const int semanas = 8;
        var edades = await dao.ObtenerEnvejecimientoAsync(filtro, cancelacion);
        var cierres = await dao.ContarCierresRecientesAsync(semanas * 7, filtro, cancelacion);
        var masAntiguas = await dao.ObtenerMasAntiguasAsync(MaximoDeMasAntiguas, filtro, cancelacion);
        var porSemana = cierres / (double)semanas;
        return new EnvejecimientoDto
        {
            Abiertas = edades.Total,
            Rangos =
            [
                new() { Rango = "hasta_3", Etiqueta = "Hasta 3 días", Cantidad = edades.HastaTresDias },
                new() { Rango = "4_a_7", Etiqueta = "4 a 7 días", Cantidad = edades.CuatroASiete },
                new() { Rango = "8_a_30", Etiqueta = "8 a 30 días", Cantidad = edades.OchoATreinta },
                new() { Rango = "31_a_90", Etiqueta = "31 a 90 días", Cantidad = edades.TreintaYUnoANoventa },
                new() { Rango = "mas_de_90", Etiqueta = "Más de 90 días", Cantidad = edades.MasDeNoventa },
            ],
            CierresPorSemana = Redondear(porSemana)!.Value,
            BacklogEnSemanas = porSemana > 0 ? Redondear(edades.Total / porSemana) : null,
            MasAntiguas = masAntiguas.Select(orden => new OrdenAntiguaDto
            {
                OrdenId = orden.OrdenId,
                NombreActivo = orden.NombreActivo,
                Cliente = orden.Cliente,
                Estado = orden.Estado,
                EdadDias = Redondear(orden.EdadDias)!.Value,
            }).ToList(),
        };
    }

    /// <summary>Intervalos entre correctivas por activo.</summary>
    [HttpGet("analisis/desgaste")]
    [Authorize(Policy = Politicas.ReporteConsultar)]
    [ProducesResponseType<DesgasteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DesgasteDto>> ObtenerDesgaste([FromQuery] FiltrosAnaliticaDto filtros, CancellationToken cancelacion)
    {
        if (RangoInvalido(filtros) is { } error)
        {
            return error;
        }

        var filtro = new FiltroAnalitica(filtros.FechaDesde, filtros.FechaHasta, filtros.ClienteId, filtros.ActivoId);
        return Ok(await cache.ObtenerAsync($"desgaste|{filtro}", token => CalcularDesgasteAsync(filtro, token), cancelacion));
    }

    private async Task<DesgasteDto> CalcularDesgasteAsync(FiltroAnalitica filtro, CancellationToken cancelacion)
    {
        var filas = await dao.ObtenerIntervalosEntreCorrectivasAsync(filtro, cancelacion);
        var activos = new List<DesgasteActivoDto>();
        foreach (var grupo in filas.GroupBy(fila => fila.ActivoId))
        {
            var intervalos = grupo.OrderBy(fila => fila.Numero).ToList();
            if (intervalos.Count < MinimoDeIntervalos)
            {
                continue;
            }

            var dias = intervalos.Select(fila => fila.Dias).ToList();
            var pendiente = Estadistica.Pendiente(dias);
            var mitad = dias.Count / 2;
            var seAcortan = pendiente < 0 && dias.Skip(dias.Count - mitad).Average() < dias.Take(mitad).Average();
            var usos = intervalos.Where(fila => fila.UsoEntreFallas is > 0).Select(fila => fila.UsoEntreFallas!.Value).ToList();
            var primero = intervalos[0];
            activos.Add(new DesgasteActivoDto
            {
                ActivoId = primero.ActivoId,
                NombreActivo = primero.NombreActivo,
                Cliente = primero.Cliente,
                Modelo = primero.Modelo,
                IntervalosDias = dias.Select(valor => Redondear(valor)!.Value).ToList(),
                PromedioDias = Redondear(dias.Average())!.Value,
                PendienteDiasPorIntervalo = Redondear(pendiente)!.Value,
                SeAcortan = seAcortan,
                UnidadUso = primero.UnidadUso,
                UsoPromedioEntreFallas = usos.Count == 0 ? null : Redondear(usos.Average()),
            });
        }

        return new DesgasteDto
        {
            MinimoDeIntervalos = MinimoDeIntervalos,
            Activos = activos.OrderByDescending(activo => activo.SeAcortan).ThenBy(activo => activo.PendienteDiasPorIntervalo)
                .Take(MaximoDeActivosConDesgaste).ToList(),
            DatosInsuficientes = activos.Count == 0,
        };
    }

    /// <summary>Avisos de ineficiencia vigentes, los críticos primero.</summary>
    [HttpGet("avisos")]
    [Authorize(Policy = Politicas.ReporteConsultar)]
    [ProducesResponseType<AvisosDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AvisosDto>> ObtenerAvisos(CancellationToken cancelacion) =>
        Ok(await cache.ObtenerAsync("avisos", avisos.EvaluarAsync, cancelacion));

    /// <summary>Reglas de los avisos con sus umbrales.</summary>
    [HttpGet("avisos/reglas")]
    [Authorize(Policy = Politicas.ReporteConsultar)]
    [ProducesResponseType<List<ReglaAvisoDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ReglaAvisoDto>>> ObtenerReglas(CancellationToken cancelacion)
    {
        var reglas = await contexto.ReglasAviso.AsNoTracking().ToListAsync(cancelacion);
        return Ok(reglas.OrderBy(regla => regla.Codigo.Length).ThenBy(regla => regla.Codigo, StringComparer.Ordinal).Select(ADto).ToList());
    }

    /// <summary>Cambia los umbrales de una regla (sin mínimos ni máximos fijos) o la enciende y apaga.</summary>
    [HttpPut("avisos/reglas/{codigo}")]
    [Authorize(Policy = Politicas.AvisoConfigurar)]
    [ProducesResponseType<ReglaAvisoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReglaAvisoDto>> ActualizarRegla(string codigo, ReglaAvisoActualizarDto solicitud, CancellationToken cancelacion)
    {
        var regla = await contexto.ReglasAviso.FirstOrDefaultAsync(item => item.Codigo == codigo, cancelacion);
        if (regla is null)
        {
            return NotFound();
        }

        if (solicitud.VentanaDias is <= 0 || solicitud.VentanaCriticaDias is <= 0 || solicitud.MinimoDatos < 0)
        {
            return this.RespuestaDeError(400, "REGLA_INVALIDA", "Las ventanas deben ser mayores que cero y el mínimo de datos no puede ser negativo.");
        }

        regla.Activa = solicitud.Activa;
        regla.Umbral = solicitud.Umbral;
        regla.UmbralCritico = solicitud.UmbralCritico;
        regla.VentanaDias = solicitud.VentanaDias;
        regla.VentanaCriticaDias = solicitud.VentanaCriticaDias;
        regla.MinimoDatos = solicitud.MinimoDatos;
        await contexto.SaveChangesAsync(cancelacion);
        return Ok(ADto(regla));
    }

    private static ReglaAvisoDto ADto(ReglaAviso regla) => new()
    {
        Codigo = regla.Codigo,
        Nombre = regla.Nombre,
        Activa = regla.Activa,
        Umbral = regla.Umbral,
        UmbralCritico = regla.UmbralCritico,
        VentanaDias = regla.VentanaDias,
        VentanaCriticaDias = regla.VentanaCriticaDias,
        MinimoDatos = regla.MinimoDatos,
    };

    private ActionResult? RangoInvalido(FiltrosAnaliticaDto filtros)
    {
        if (filtros.FechaDesde is not null && filtros.FechaHasta is not null && filtros.FechaDesde > filtros.FechaHasta)
        {
            return this.RespuestaDeError(400, "RANGO_FECHAS_INVALIDO", "La fecha desde no puede ser posterior a la fecha hasta.");
        }

        return null;
    }

    private static double? Redondear(double? valor) => valor is null ? null : Math.Round(valor.Value, 2);
}
