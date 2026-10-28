using System.Globalization;
using System.Text.Json;
using Api.DTOs;
using Api.DTOs.Reportes;
using Api.Infraestructura;
using Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

[ApiController]
[Route("api/v1/reportes/demoras-etapas")]
public class DemorasEtapasController(SotraContext contexto, DemorasEtapasDAO dao, CacheDeAnalitica cache) : ControllerBase
{
    private static readonly string[] Etapas = ["pendiente", "en_progreso", "revision", "pausa"];

    /// <summary>Tiempo acumulado por orden y etapa, referencias históricas y página de demoras. No incluye personas.</summary>
    [HttpGet]
    [Authorize(Policy = Politicas.ReporteConsultar)]
    [ProducesResponseType<DemorasEtapasDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DemorasEtapasDto>> Obtener([FromQuery] FiltrosAnaliticaDto filtros,
        [FromQuery] string? etapa, [FromQuery] int pagina = 1, [FromQuery] int tamanoPagina = 20, CancellationToken cancelacion = default)
    {
        if (filtros.FechaDesde > filtros.FechaHasta)
            return this.RespuestaDeError(400, "RANGO_FECHAS_INVALIDO", "La fecha desde no puede ser posterior a la fecha hasta.");
        if (etapa is not null && !Etapas.Contains(etapa))
            return this.RespuestaDeError(400, "ETAPA_INVALIDA", "La etapa debe ser pendiente, en_progreso, revision o pausa.");
        if (pagina < 1 || tamanoPagina < 1 || tamanoPagina > PaginaDto<OrdenDemoradaEtapaDto>.TamanoPaginaMaximo)
            return this.RespuestaDeError(400, "PAGINACION_INVALIDA", "La página debe ser mayor que cero y el tamaño debe estar entre 1 y 100.");

        var umbrales = await contexto.UmbralesEtapaOt.AsNoTracking().OrderBy(u => u.Etapa).ToListAsync(cancelacion);
        // El valor de configuración forma parte de la clave: guardar/restablecer tiene efecto inmediato.
        var configuracion = string.Join("|", umbrales.Select(u => u.Etapa + ":" + u.UmbralHoras?.ToString("R", CultureInfo.InvariantCulture)));
        var filtro = new FiltroAnalitica(filtros.FechaDesde, filtros.FechaHasta, filtros.ClienteId, filtros.ActivoId);
        var clave = $"demoras-etapas|{filtro}|{etapa}|{pagina}|{tamanoPagina}|{configuracion}";
        return Ok(await cache.ObtenerAsync(clave, async token =>
            JsonSerializer.Deserialize<DemorasEtapasDto>(await dao.ObtenerAsync(filtro, etapa, pagina, tamanoPagina, DateTime.UtcNow, token))!, cancelacion));
    }

    /// <summary>Las cuatro referencias de duración editables por el administrador.</summary>
    [HttpGet("umbrales")]
    [Authorize(Policy = Politicas.AvisoConfigurar)]
    [ProducesResponseType<List<UmbralEtapaDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UmbralEtapaDto>>> ObtenerUmbrales(CancellationToken cancelacion) =>
        Ok(await contexto.UmbralesEtapaOt.AsNoTracking().OrderBy(u => u.Etapa)
            .Select(u => new UmbralEtapaDto { Etapa = u.Etapa, UmbralHoras = u.UmbralHoras }).ToListAsync(cancelacion));

    /// <summary>Guarda horas (incluido cero); null restablece la referencia histórica. Solo administrador.</summary>
    [HttpPut("umbrales/{etapa}")]
    [Authorize(Policy = Politicas.AvisoConfigurar)]
    [ProducesResponseType<UmbralEtapaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UmbralEtapaDto>> GuardarUmbral(string etapa, ActualizarUmbralEtapaDto solicitud, CancellationToken cancelacion)
    {
        if (solicitud.UmbralHoras is { } horas && (!double.IsFinite(horas) || horas < 0))
            return this.RespuestaDeError(400, "UMBRAL_ETAPA_INVALIDO", "El umbral debe ser un número de horas no negativo o null para usar el promedio histórico.");
        var registro = await contexto.UmbralesEtapaOt.SingleOrDefaultAsync(u => u.Etapa == etapa, cancelacion);
        if (registro is null)
            return this.RespuestaDeError(404, "ETAPA_NO_ENCONTRADA", "No se encontró la etapa.");
        registro.UmbralHoras = solicitud.UmbralHoras;
        await contexto.SaveChangesAsync(cancelacion);
        return Ok(new UmbralEtapaDto { Etapa = registro.Etapa, UmbralHoras = registro.UmbralHoras });
    }
}
