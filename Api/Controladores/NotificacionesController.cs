using Api.DTOs;
using Api.DTOs.Notificaciones;
using Api.Infraestructura;
using Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SOTRA_BACKEND.Modelos;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

// Notificaciones operativas (RFG-05): avisos dentro de la aplicación. Cada persona ve y marca solo los suyos; sin sesión
// (autenticación apagada) no hay destinatario y las consultas responden vacío.
[ApiController]
[Authorize(Policy = Politicas.SesionIniciada)]
[Route("api/v1/notificaciones")]
public class NotificacionesController(NotificacionDAO notificacionDao, UsuarioActual usuarioActual) : ControllerBase
{
    /// <summary>Lista las notificaciones de la persona con sesión, las más nuevas primero.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginaDto<NotificacionDto>>> Listar([FromQuery] FiltrosNotificacionesDto filtros, CancellationToken cancelacion)
    {
        if (!PaginaDto<NotificacionDto>.EsValida(filtros.Pagina, filtros.TamanoPagina))
        {
            return this.PaginacionInvalida();
        }

        if (await usuarioActual.ObtenerIdUsuarioAsync(cancelacion) is not { } usuarioId)
        {
            return Ok(PaginaDto<NotificacionDto>.Crear([], 0, filtros.Pagina, filtros.TamanoPagina));
        }

        var (elementos, total) = await notificacionDao.ListarPaginaAsync(
            usuarioId, filtros.SoloNoLeidas, filtros.Pagina, filtros.TamanoPagina, cancelacion);
        return Ok(PaginaDto<NotificacionDto>.Crear(elementos.Select(ADto).ToList(), total, filtros.Pagina, filtros.TamanoPagina));
    }

    /// <summary>Cuenta las notificaciones sin leer de la persona con sesión (el número de la campana).</summary>
    [HttpGet("no-leidas")]
    public async Task<ActionResult<NoLeidasDto>> ContarNoLeidas(CancellationToken cancelacion)
    {
        if (await usuarioActual.ObtenerIdUsuarioAsync(cancelacion) is not { } usuarioId)
        {
            return Ok(new NoLeidasDto());
        }

        return Ok(new NoLeidasDto { Total = await notificacionDao.ContarNoLeidasAsync(usuarioId, cancelacion) });
    }

    /// <summary>Marca como leída una notificación propia. Es idempotente.</summary>
    [HttpPatch("{id:long}/leida")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarcarLeida(long id, CancellationToken cancelacion)
    {
        if (await usuarioActual.ObtenerIdUsuarioAsync(cancelacion) is not { } usuarioId
            || !await notificacionDao.MarcarLeidaAsync(usuarioId, id, cancelacion))
        {
            return NotFound();
        }

        return NoContent();
    }

    /// <summary>Marca como leídas todas las notificaciones de la persona con sesión.</summary>
    [HttpPost("marcar-leidas")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarcarTodasLeidas(CancellationToken cancelacion)
    {
        if (await usuarioActual.ObtenerIdUsuarioAsync(cancelacion) is { } usuarioId)
        {
            await notificacionDao.MarcarTodasLeidasAsync(usuarioId, cancelacion);
        }

        return NoContent();
    }

    private static NotificacionDto ADto(Notificacion notificacion) => new()
    {
        Id = notificacion.IdNotificacion,
        Tipo = notificacion.Tipo,
        Mensaje = notificacion.Mensaje,
        OrdenId = notificacion.IdOrden,
        CreadaEn = notificacion.CreadaEn,
        Leida = notificacion.LeidaEn is not null,
    };
}
