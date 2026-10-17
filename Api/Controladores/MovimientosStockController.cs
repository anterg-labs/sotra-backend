using Api.DTOs;
using Api.DTOs.MovimientosStock;
using Api.Mapeadores;
using Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

[ApiController]
[Route("api/v1/movimientos-stock")]
public class MovimientosStockController : ControllerBase
{
    private readonly MovimientoStockDAO _movimientoStockDao;

    public MovimientosStockController(MovimientoStockDAO movimientoStockDao)
    {
        _movimientoStockDao = movimientoStockDao;
    }

    [Authorize(Policy = Politicas.StockConsultar)]
    [HttpGet]
    public ActionResult<PaginaDto<MovimientoStockDto>> ObtenerTodos([FromQuery] FiltrosMovimientosStockDto filtros)
    {
        if (!PaginaDto<MovimientoStockDto>.EsValida(filtros.Pagina, filtros.TamanoPagina))
        {
            return BadRequest(PaginaDto<MovimientoStockDto>.ErrorDePaginacion());
        }

        if (filtros.FechaDesde is not null && filtros.FechaHasta is not null
            && filtros.FechaDesde > filtros.FechaHasta)
        {
            return BadRequest(new
            {
                codigo = "RANGO_FECHAS_INVALIDO",
                mensaje = "La fecha desde no puede ser posterior a la fecha hasta."
            });
        }

        var textoBuscado = "";
        if (!string.IsNullOrWhiteSpace(filtros.Buscar))
        {
            textoBuscado = filtros.Buscar.Trim().ToLower();
        }

        var ubicacionBuscada = "";
        if (!string.IsNullOrWhiteSpace(filtros.Ubicacion))
        {
            ubicacionBuscada = filtros.Ubicacion.Trim().ToLower();
        }

        var respuesta = new List<MovimientoStockDto>();
        foreach (var movimiento in _movimientoStockDao.ListarTodo())
        {
            var movimientoDto = MovimientoStockMapeador.ADto(movimiento);

            if (textoBuscado != "")
            {
                var coincide = PaginaDto<MovimientoStockDto>.Contiene(movimientoDto.CodigoRepuesto, textoBuscado)
                               || PaginaDto<MovimientoStockDto>.Contiene(movimientoDto.NombreRepuesto, textoBuscado)
                               || PaginaDto<MovimientoStockDto>.Contiene(movimientoDto.Motivo, textoBuscado);
                if (!coincide)
                {
                    continue;
                }
            }

            if (filtros.RepuestoId is not null && movimientoDto.RepuestoId != filtros.RepuestoId)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(filtros.Tipo) && movimientoDto.Tipo != filtros.Tipo)
            {
                continue;
            }

            var fechaMovimiento = DateOnly.FromDateTime(movimientoDto.Fecha);
            if (filtros.FechaDesde is not null && fechaMovimiento < filtros.FechaDesde)
            {
                continue;
            }

            if (filtros.FechaHasta is not null && fechaMovimiento > filtros.FechaHasta)
            {
                continue;
            }

            if (ubicacionBuscada != "" && !PaginaDto<MovimientoStockDto>.Contiene(movimientoDto.Ubicacion, ubicacionBuscada))
            {
                continue;
            }

            if (filtros.UsuarioId is not null && movimientoDto.UsuarioId != filtros.UsuarioId)
            {
                continue;
            }

            respuesta.Add(movimientoDto);
        }

        return Ok(PaginaDto<MovimientoStockDto>.Crear(respuesta, filtros.Pagina, filtros.TamanoPagina));
    }

    [Authorize(Policy = Politicas.StockConsultar)]
    [HttpGet("{id:long}")]
    public ActionResult<MovimientoStockDto> ObtenerPorId(long id)
    {
        var movimiento = _movimientoStockDao.Seleccionar(id);
        if (movimiento is null)
        {
            return NotFound();
        }

        return Ok(MovimientoStockMapeador.ADto(movimiento));
    }
}
