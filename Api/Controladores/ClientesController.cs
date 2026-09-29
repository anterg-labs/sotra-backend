using Api.DTOs.Clientes;
using Api.Mapeadores;
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

    [HttpGet]
    [ProducesResponseType<List<ClienteListadoDto>>(StatusCodes.Status200OK)]
    public ActionResult<List<ClienteListadoDto>> ObtenerTodos()
    {
        var respuesta = new List<ClienteListadoDto>();
        foreach (var cliente in _clienteDao.ListarTodo())
        {
            respuesta.Add(ClienteMapeador.AListadoDto(cliente));
        }

        return Ok(respuesta);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType<ClienteListadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ClienteListadoDto> ObtenerPorId(long id)
    {
        var cliente = _clienteDao.SeleccionarCliente(id);
        return cliente is null ? NotFound() : Ok(ClienteMapeador.AListadoDto(cliente));
    }

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
