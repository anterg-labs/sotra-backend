using Api.Infraestructura;
using Api.DTOs;
using Api.DTOs.Clientes;
using Api.Mapeadores;
using Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SOTRA_BACKEND.Operaciones;
using SOTRA_BACKEND.Modelos;

namespace Api.Controladores;

[ApiController]
[Route("api/v1/clientes")]
public class ClientesController : ControllerBase
{
    private readonly ClienteDAO _clienteDao;

    public ClientesController(ClienteDAO clienteDao)
    {
        _clienteDao = clienteDao;
    }

    [HttpGet]
    [Authorize(Policy = Politicas.ClienteConsultar)]
    [ProducesResponseType<PaginaDto<ClienteListadoDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaDto<ClienteListadoDto>>> ObtenerTodos([FromQuery] FiltrosClientesDto filtros, CancellationToken cancelacion)
    {
        if (!PaginaDto<ClienteListadoDto>.EsValida(filtros.Pagina, filtros.TamanoPagina))
        {
            return BadRequest(PaginaDto<ClienteListadoDto>.ErrorDePaginacion());
        }

        if (!ClienteDAO.ColumnasOrdenables.Contiene(filtros.OrdenarPor))
        {
            return this.OrdenInvalido(ClienteDAO.ColumnasOrdenables.Claves);
        }

        if (!ConsultasDeTexto.TryTraducirEstado(filtros.Estado, out var estado))
        {
            return Ok(PaginaDto<ClienteListadoDto>.Crear([], 0, filtros.Pagina, filtros.TamanoPagina));
        }

        var filtro = new FiltroClientes(filtros.Buscar, estado, filtros.DireccionOrden == "desc", filtros.OrdenarPor);
        var (entidades, total) = await _clienteDao.ListarPaginaAsync(filtro, filtros.Pagina, filtros.TamanoPagina, cancelacion);
        var cantidades = await _clienteDao.ContarOrdenesAsociadasAsync(entidades.Select(c => c.IdCliente), cancelacion);
        var elementos = entidades.Select(c => ClienteMapeador.AListadoDto(c, cantidades.GetValueOrDefault(c.IdCliente))).ToList();
        return Ok(PaginaDto<ClienteListadoDto>.Crear(elementos, total, filtros.Pagina, filtros.TamanoPagina));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = Politicas.ClienteConsultar)]
    [ProducesResponseType<ClienteListadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClienteListadoDto>> ObtenerPorId(long id, CancellationToken cancelacion)
    {
        var cliente = await _clienteDao.SeleccionarClienteAsync(id, cancelacion);
        return cliente is null ? NotFound() : Ok(await CrearRespuestaAsync(cliente, cancelacion));
    }

    [HttpPost]
    [Authorize(Policy = Politicas.ClienteGestionar)]
    [ProducesResponseType<ClienteListadoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClienteListadoDto>> Crear(ClienteCrearDto solicitud, CancellationToken cancelacion)
    {
        var cliente = ClienteMapeador.AEntidad(solicitud);
        var clienteCreado = await _clienteDao.InsertarClienteAsync(cliente, cancelacion);
        var respuesta = ClienteMapeador.AListadoDto(clienteCreado);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = clienteCreado.IdCliente }, respuesta);
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = Politicas.ClienteGestionar)]
    [ProducesResponseType<ClienteListadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClienteListadoDto>> Actualizar(long id, ClienteActualizarDto solicitud, CancellationToken cancelacion)
    {
        var cliente = await _clienteDao.SeleccionarClienteAsync(id, cancelacion);
        if (cliente is null) return NotFound();

        ClienteMapeador.AplicarCambios(cliente, solicitud);
        await _clienteDao.ActualizarClienteAsync(cliente, cancelacion);

        return Ok(await CrearRespuestaAsync(cliente, cancelacion));
    }

    [HttpPatch("{id:long}")]
    [Authorize(Policy = Politicas.ClienteGestionar)]
    [ProducesResponseType<ClienteListadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClienteListadoDto>> CambiarEstado(long id, ClienteEstadoDto solicitud, CancellationToken cancelacion)
    {
        if (solicitud.Estado == "activo")
        {
            if (!await _clienteDao.ReactivarClienteAsync(id, cancelacion)) return NotFound();
        }
        else if (solicitud.Estado == "inactivo")
        {
            if (!await _clienteDao.DarBajaLogicaClienteAsync(id, cancelacion)) return NotFound();
        }
        else
        {
            return this.RespuestaDeError(400, "ESTADO_INVALIDO", "El estado debe ser 'activo' o 'inactivo'.");
        }

        var cliente = (await _clienteDao.SeleccionarClienteAsync(id, cancelacion))!;
        return Ok(await CrearRespuestaAsync(cliente, cancelacion));
    }

    private async Task<ClienteListadoDto> CrearRespuestaAsync(Cliente cliente, CancellationToken cancelacion)
    {
        var cantidades = await _clienteDao.ContarOrdenesAsociadasAsync([cliente.IdCliente], cancelacion);
        return ClienteMapeador.AListadoDto(cliente, cantidades.GetValueOrDefault(cliente.IdCliente));
    }
}
