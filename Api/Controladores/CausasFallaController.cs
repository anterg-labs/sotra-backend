using Api.DTOs.CausasFalla;
using Api.Infraestructura;
using Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SOTRA_BACKEND.Modelos;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

[ApiController]
[Route("api/v1/causas-falla")]
public class CausasFallaController(CausaFallaDAO causaFallaDao) : ControllerBase
{
    /// <summary>Lista el catálogo de causas de falla, ordenado por nombre, con filtro opcional por estado.</summary>
    [HttpGet]
    [Authorize(Policy = Politicas.OtConsultar)]
    [ProducesResponseType<List<CausaFallaDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CausaFallaDto>>> ObtenerTodas([FromQuery] string? estado, CancellationToken cancelacion)
    {
        if (!ConsultasDeTexto.TryTraducirEstado(estado, out var filtro))
        {
            return Ok(new List<CausaFallaDto>());
        }

        var causas = await causaFallaDao.ListarAsync(filtro, cancelacion);
        return Ok(causas.Select(ADto).ToList());
    }

    /// <summary>Registra una causa de falla.</summary>
    [HttpPost]
    [Authorize(Policy = Politicas.TipoMaquinaGestionar)]
    [ProducesResponseType<CausaFallaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CausaFallaDto>> Crear(CausaFallaCrearDto solicitud, CancellationToken cancelacion)
    {
        var causa = await causaFallaDao.InsertarAsync(solicitud.Nombre.Trim(), cancelacion);
        return CreatedAtAction(nameof(ObtenerTodas), new { }, ADto(causa));
    }

    /// <summary>Cambia el nombre de una causa de falla.</summary>
    [HttpPut("{id:long}")]
    [Authorize(Policy = Politicas.TipoMaquinaGestionar)]
    [ProducesResponseType<CausaFallaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CausaFallaDto>> Actualizar(long id, CausaFallaCrearDto solicitud, CancellationToken cancelacion)
    {
        if (!await causaFallaDao.ActualizarNombreAsync(id, solicitud.Nombre.Trim(), cancelacion))
        {
            return NotFound();
        }

        return Ok(ADto((await causaFallaDao.SeleccionarAsync(id, cancelacion))!));
    }

    /// <summary>Activa o desactiva una causa de falla (las inactivas no se ofrecen en las órdenes nuevas).</summary>
    [HttpPatch("{id:long}/estado")]
    [Authorize(Policy = Politicas.TipoMaquinaGestionar)]
    [ProducesResponseType<CausaFallaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CausaFallaDto>> CambiarEstado(long id, CausaFallaEstadoDto solicitud, CancellationToken cancelacion)
    {
        if (solicitud.Estado is not ("activo" or "inactivo"))
        {
            return this.RespuestaDeError(400, "ESTADO_INVALIDO", "El estado debe ser 'activo' o 'inactivo'.");
        }

        var causa = await causaFallaDao.CambiarEstadoAsync(id, solicitud.Estado == "activo", cancelacion);
        return causa is null ? NotFound() : Ok(ADto(causa));
    }

    private static CausaFallaDto ADto(CausaFalla causa) => new()
    {
        Id = causa.IdCausaFalla,
        Nombre = causa.Nombre,
        Estado = causa.Estado ? "activo" : "inactivo"
    };
}
