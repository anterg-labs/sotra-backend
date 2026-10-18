using Api.DTOs.Terminos;
using Api.Infraestructura.Seguridad;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using SOTRA_BACKEND.Modelos;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

/// <summary>Un único texto de términos y condiciones y su aceptación por persona usuaria.</summary>
[ApiController]
[Route("api/v1/terminos")]
public class TerminosController(TerminosDAO terminosDao) : ControllerBase
{

    [HttpGet("vigente")]
    [ProducesResponseType<TerminosDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TerminosDto>> ObtenerVigente(CancellationToken cancelacion)
    {
        var vigente = await terminosDao.ObtenerVigenteAsync(cancelacion);
        return vigente is null ? NotFound() : Ok(ADto(vigente));
    }

    [HttpPost]
    [Permiso("terminos.administrar")]
    [ProducesResponseType<TerminosDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TerminosDto>> Publicar(TerminosPublicarDto solicitud, CancellationToken cancelacion)
    {
        var publicada = await terminosDao.PublicarAsync(
            solicitud.Texto.Trim(), cancelacion);
        return Ok(ADto(publicada));
    }

    [HttpGet("estado")]
    [ProducesResponseType<EstadoTerminosDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EstadoTerminosDto>> ObtenerEstado(
        [FromQuery, Range(1, long.MaxValue)] long usuarioId, CancellationToken cancelacion) =>
        Ok(await ConstruirEstado(usuarioId, cancelacion));

    private async Task<EstadoTerminosDto> ConstruirEstado(long usuarioId, CancellationToken cancelacion)
    {
        var estado = await terminosDao.ObtenerEstadoAsync(usuarioId, cancelacion);
        return new EstadoTerminosDto
        {
            Vigente = estado.Vigente is null ? null : ADto(estado.Vigente),
            Pendiente = estado.Vigente is not null && !estado.AceptoLaVigente,
            VersionAceptada = estado.VersionAceptada,
            AceptadoEn = estado.AceptadoEn,
        };
    }

    [HttpPost("aceptaciones")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Aceptar(AceptacionTerminosDto solicitud, CancellationToken cancelacion)
    {
        if (solicitud.Acepta != true)
        {
            return BadRequest(new
            {
                codigo = "ACEPTACION_OBLIGATORIA",
                mensaje = "Hay que marcar que se leyeron y aceptaron los términos."
            });
        }

        return await terminosDao.AceptarAsync(solicitud.UsuarioId, solicitud.TerminosId, solicitud.Version, cancelacion) switch
        {
            ResultadoDeAceptacion.Registrada => StatusCode(StatusCodes.Status201Created, await ConstruirEstado(solicitud.UsuarioId, cancelacion)),
            ResultadoDeAceptacion.YaAceptada => Ok(await ConstruirEstado(solicitud.UsuarioId, cancelacion)),
            ResultadoDeAceptacion.UsuarioInexistente => NotFound(new
            {
                codigo = "USUARIO_INEXISTENTE",
                mensaje = "La persona usuaria no existe."
            }),
            _ => Conflict(new
            {
                codigo = "TERMINOS_DESACTUALIZADOS",
                mensaje = "Los términos cambiaron mientras los leías. Volvé a leer la versión vigente."
            }),
        };
    }

    private static TerminosDto ADto(TerminosCondiciones terminos) => new()
    {
        Id = terminos.IdTerminos,
        Version = terminos.Version,
        Texto = terminos.Texto,
        Vigente = terminos.Vigente,
        CreadoEn = terminos.CreadoEn,
    };
}
