using Api.DTOs;
using Api.DTOs.Clientes;
using Api.Mapeadores;
using Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Operaciones;

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

    [Authorize(Policy = Politicas.ClienteConsultar)]
    [HttpGet]
    [ProducesResponseType<PaginaDto<ClienteListadoDto>>(StatusCodes.Status200OK)]
    public ActionResult<PaginaDto<ClienteListadoDto>> ObtenerTodos([FromQuery] FiltrosClientesDto filtros)
    {
        if (!PaginaDto<ClienteListadoDto>.EsValida(filtros.Pagina, filtros.TamanoPagina))
        {
            return BadRequest(PaginaDto<ClienteListadoDto>.ErrorDePaginacion());
        }

        var textoBuscado = "";
        if (!string.IsNullOrWhiteSpace(filtros.Buscar))
        {
            textoBuscado = filtros.Buscar.Trim().ToLower();
        }

        var respuesta = new List<ClienteListadoDto>();
        foreach (var cliente in _clienteDao.ListarTodo())
        {
            var clienteDto = ClienteMapeador.AListadoDto(cliente);

            if (textoBuscado != "")
            {
                var coincide = PaginaDto<ClienteListadoDto>.Contiene(clienteDto.RazonSocial, textoBuscado)
                               || PaginaDto<ClienteListadoDto>.Contiene(clienteDto.NombreContacto, textoBuscado)
                               || PaginaDto<ClienteListadoDto>.Contiene(clienteDto.Correo, textoBuscado);
                if (!coincide)
                {
                    continue;
                }
            }

            if (!string.IsNullOrEmpty(filtros.Estado) && clienteDto.Estado != filtros.Estado)
            {
                continue;
            }

            respuesta.Add(clienteDto);
        }

        if (filtros.DireccionOrden == "desc")
        {
            respuesta.Reverse();
        }

        return Ok(PaginaDto<ClienteListadoDto>.Crear(respuesta, filtros.Pagina, filtros.TamanoPagina));
    }

    [Authorize(Policy = Politicas.ClienteConsultar)]
    [HttpGet("{id:long}")]
    [ProducesResponseType<ClienteListadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ClienteListadoDto> ObtenerPorId(long id)
    {
        var cliente = _clienteDao.SeleccionarCliente(id);
        return cliente is null ? NotFound() : Ok(ClienteMapeador.AListadoDto(cliente));
    }

    [Authorize(Policy = Politicas.ClienteGestionar)]
    [HttpPost]
    [ProducesResponseType<ClienteListadoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public ActionResult<ClienteListadoDto> Crear(ClienteCrearDto solicitud)
    {
        try
        {
            var cliente = ClienteMapeador.AEntidad(solicitud);
            var clienteCreado = _clienteDao.InsertarCliente(cliente);
            var respuesta = ClienteMapeador.AListadoDto(clienteCreado);
            return CreatedAtAction(nameof(ObtenerPorId), new { id = clienteCreado.IdCliente }, respuesta);
        }
        catch (DbUpdateException)
        {
            return Conflict(new
            {
                mensaje = "Ya existe un cliente con la razón social, nombre, correo o teléfono indicado."
            });
        }
    }

    [Authorize(Policy = Politicas.ClienteGestionar)]
    [HttpPut("{id:long}")]
    [ProducesResponseType<ClienteListadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public ActionResult<ClienteListadoDto> Actualizar(long id, ClienteActualizarDto solicitud)
    {
        try
        {
            var cliente = _clienteDao.SeleccionarCliente(id);
            if (cliente is null) return NotFound();

            ClienteMapeador.AplicarCambios(cliente, solicitud);
            _clienteDao.ActualizarCliente(cliente);

            return Ok(ClienteMapeador.AListadoDto(cliente));
        }
        catch (DbUpdateException)
        {
            return Conflict(new
            {
                mensaje = "Ya existe un cliente con la razón social, nombre, correo o teléfono indicado."
            });
        }
    }

    [Authorize(Policy = Politicas.ClienteGestionar)]
    [HttpPatch("{id:long}")]
    [ProducesResponseType<ClienteListadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ClienteListadoDto> CambiarEstado(long id, ClienteEstadoDto solicitud)
    {
        if (solicitud.Estado == "activo")
        {
            if (!_clienteDao.ReactivarCliente(id)) return NotFound();
        }
        else if (solicitud.Estado == "inactivo")
        {
            if (!_clienteDao.DarBajaLogicaCliente(id)) return NotFound();
        }
        else
        {
            return BadRequest(new { mensaje = "El estado debe ser 'activo' o 'inactivo'." });
        }

        var cliente = _clienteDao.SeleccionarCliente(id)!;
        return Ok(ClienteMapeador.AListadoDto(cliente));
    }
}
