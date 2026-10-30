using Api.Infraestructura;
using Api.DTOs;
using Api.DTOs.ModelosMaquina;
using Api.Mapeadores;
using Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

[ApiController]
[Route("api/v1/modelos-maquina")]
public class ModelosMaquinaController : ControllerBase
{
    private readonly ModeloMaquinaDAO _modeloMaquinaDao;

    public ModelosMaquinaController(ModeloMaquinaDAO modeloMaquinaDao)
    {
        _modeloMaquinaDao = modeloMaquinaDao;
    }

    /// <summary>Lista los modelos de máquina con filtros por texto, tipo y estado.</summary>
    [HttpGet]
    [Authorize(Policy = Politicas.ModeloConsultar)]
    [ProducesResponseType<PaginaDto<ModeloMaquinaDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaDto<ModeloMaquinaDto>>> ObtenerTodos([FromQuery] FiltrosModelosMaquinaDto filtros, CancellationToken cancelacion)
    {
        if (!PaginaDto<ModeloMaquinaDto>.EsValida(filtros.Pagina, filtros.TamanoPagina))
        {
            return this.PaginacionInvalida();
        }

        if (!ModeloMaquinaDAO.ColumnasOrdenables.Contiene(filtros.OrdenarPor))
        {
            return this.OrdenInvalido(ModeloMaquinaDAO.ColumnasOrdenables.Claves);
        }

        if (!ConsultasDeTexto.TryTraducirEstado(filtros.Estado, out var estado))
        {
            return Ok(PaginaDto<ModeloMaquinaDto>.Crear([], 0, filtros.Pagina, filtros.TamanoPagina));
        }

        var filtro = new FiltroModelosMaquina(filtros.Buscar, filtros.TipoMaquinaId, estado, filtros.OrdenarPor, filtros.DireccionOrden == "desc");
        var (entidades, total) = await _modeloMaquinaDao.ListarPaginaAsync(filtro, filtros.Pagina, filtros.TamanoPagina, cancelacion);
        var elementos = entidades.Select(ModeloMaquinaMapeador.ADto).ToList();
        return Ok(PaginaDto<ModeloMaquinaDto>.Crear(elementos, total, filtros.Pagina, filtros.TamanoPagina));
    }

    /// <summary>Devuelve un modelo de máquina por su id.</summary>
    [HttpGet("{id:long}")]
    [Authorize(Policy = Politicas.ModeloConsultar)]
    [ProducesResponseType<ModeloMaquinaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ModeloMaquinaDto>> ObtenerPorId(long id, CancellationToken cancelacion)
    {
        var modelo = await _modeloMaquinaDao.SeleccionarModeloMaquinaAsync(id, cancelacion);
        return modelo is null ? NotFound() : Ok(ModeloMaquinaMapeador.ADto(modelo));
    }

    /// <summary>Registra un modelo de máquina.</summary>
    [HttpPost]
    [Authorize(Policy = Politicas.ModeloCrear)]
    [ProducesResponseType<ModeloMaquinaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ModeloMaquinaDto>> Crear(ModeloMaquinaCrearDto solicitud, CancellationToken cancelacion)
    {
        var modeloCreado = await _modeloMaquinaDao.InsertarModeloMaquinaAsync(
            ModeloMaquinaMapeador.AEntidad(solicitud), cancelacion);
        var respuesta = ModeloMaquinaMapeador.ADto(modeloCreado);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = modeloCreado.IdModeloMaquina }, respuesta);
    }

    /// <summary>Modifica un modelo de máquina.</summary>
    [HttpPut("{id:long}")]
    [Authorize(Policy = Politicas.ModeloEditar)]
    [ProducesResponseType<ModeloMaquinaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ModeloMaquinaDto>> Actualizar(long id, ModeloMaquinaActualizarDto solicitud, CancellationToken cancelacion)
    {
        var modelo = await _modeloMaquinaDao.SeleccionarModeloMaquinaAsync(id, cancelacion);
        if (modelo is null) return NotFound();

        ModeloMaquinaMapeador.AplicarCambios(modelo, solicitud);
        await _modeloMaquinaDao.ActualizarModeloMaquinaAsync(modelo, cancelacion);
        return Ok(ModeloMaquinaMapeador.ADto(modelo));
    }

    /// <summary>Cambia el estado de un modelo (baja o reactivación lógica).</summary>
    [HttpPatch("{id:long}")]
    [Authorize(Policy = Politicas.ModeloEditar)]
    [ProducesResponseType<ModeloMaquinaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ModeloMaquinaDto>> Desactivar(long id, ModeloMaquinaEstadoDto solicitud, CancellationToken cancelacion)
    {
        if (!string.Equals(solicitud.Estado, "inactivo", StringComparison.OrdinalIgnoreCase))
            return this.RespuestaDeError(400, "ESTADO_INVALIDO", "Este endpoint solo acepta el estado 'inactivo'.");

        if (!await _modeloMaquinaDao.DarBajaLogicaModeloMaquinaAsync(id, cancelacion)) return NotFound();

        var modelo = (await _modeloMaquinaDao.SeleccionarModeloMaquinaAsync(id, cancelacion))!;
        return Ok(ModeloMaquinaMapeador.ADto(modelo));
    }
}
