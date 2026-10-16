using Api.DTOs;
using Api.DTOs.Clientes;
using Api.Mapeadores;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
        catch (DbUpdateException excepcion)
        {
            return Conflict(MensajeDeDuplicado(excepcion));
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
        catch (DbUpdateException excepcion)
        {
            return Conflict(MensajeDeDuplicado(excepcion));
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

    private static object MensajeDeDuplicado(DbUpdateException excepcion)
    {
        var restriccion = (excepcion.InnerException as PostgresException)?.ConstraintName;
        return restriccion switch
        {
            "UK_RAZON_SOCIAL_CLIENTE" => new { codigo = "RAZON_SOCIAL_DUPLICADA", mensaje = "El nombre o razón social ya está registrado." },
            "UK_NOMBRE_CLIENTE" => new { codigo = "NOMBRE_CONTACTO_DUPLICADO", mensaje = "El nombre de contacto ya está registrado." },
            "UK_CORREO_CLIENTE" => new { codigo = "CORREO_DUPLICADO", mensaje = "El correo electrónico ya está registrado." },
            "UK_TELEFONO_CLIENTE" => new { codigo = "TELEFONO_DUPLICADO", mensaje = "El número telefónico ya está registrado." },
            _ => new { codigo = "CLIENTE_DUPLICADO", mensaje = "Ya existe un cliente con la razón social, nombre, correo o teléfono indicado." },
        };
    }
}
