using Api.Infraestructura;
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

    /// <summary>Lista los movimientos de stock con filtros por repuesto, tipo, fecha, ubicación y usuario.</summary>
    [HttpGet]
    [Authorize(Policy = Politicas.StockConsultar)]
    public async Task<ActionResult<PaginaDto<MovimientoStockDto>>> ObtenerTodos([FromQuery] FiltrosMovimientosStockDto filtros, CancellationToken cancelacion)
    {
        if (!PaginaDto<MovimientoStockDto>.EsValida(filtros.Pagina, filtros.TamanoPagina))
        {
            return BadRequest(PaginaDto<MovimientoStockDto>.ErrorDePaginacion());
        }

        if (filtros.FechaDesde is not null && filtros.FechaHasta is not null
            && filtros.FechaDesde > filtros.FechaHasta)
        {
            return this.RespuestaDeError(400, "RANGO_FECHAS_INVALIDO", "La fecha desde no puede ser posterior a la fecha hasta.");
        }

        if (!MovimientoStockDAO.ColumnasOrdenables.Contiene(filtros.OrdenarPor))
        {
            return this.OrdenInvalido(MovimientoStockDAO.ColumnasOrdenables.Claves);
        }

        var filtro = new FiltroMovimientosStock(filtros.Buscar, filtros.RepuestoId, filtros.Tipo, filtros.FechaDesde, filtros.FechaHasta, filtros.Ubicacion, filtros.UsuarioId,
            filtros.OrdenarPor, filtros.DireccionOrden == "desc");
        var (entidades, total) = await _movimientoStockDao.ListarPaginaAsync(filtro, filtros.Pagina, filtros.TamanoPagina, cancelacion);
        var elementos = entidades.Select(MovimientoStockMapeador.ADto).ToList();
        return Ok(PaginaDto<MovimientoStockDto>.Crear(elementos, total, filtros.Pagina, filtros.TamanoPagina));
    }

    /// <summary>Devuelve un movimiento de stock por su id.</summary>
    [HttpGet("{id:long}")]
    [Authorize(Policy = Politicas.StockConsultar)]
    public async Task<ActionResult<MovimientoStockDto>> ObtenerPorId(long id, CancellationToken cancelacion)
    {
        var movimiento = await _movimientoStockDao.SeleccionarAsync(id, cancelacion);
        if (movimiento is null)
        {
            return NotFound();
        }

        return Ok(MovimientoStockMapeador.ADto(movimiento));
    }
}
