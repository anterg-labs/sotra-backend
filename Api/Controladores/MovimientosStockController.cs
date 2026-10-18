using Api.Infraestructura.Seguridad;
using Api.DTOs;
using Api.DTOs.MovimientosStock;
using Api.Mapeadores;
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

    [HttpGet]
    [Permiso("stock.consultar")]
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

        var filtro = new FiltroMovimientosStock(filtros.Buscar, filtros.RepuestoId, filtros.Tipo, filtros.FechaDesde, filtros.FechaHasta, filtros.Ubicacion, filtros.UsuarioId);
        var (entidades, total) = _movimientoStockDao.ListarPagina(filtro, filtros.Pagina, filtros.TamanoPagina);
        var elementos = entidades.Select(MovimientoStockMapeador.ADto).ToList();
        return Ok(PaginaDto<MovimientoStockDto>.Crear(elementos, total, filtros.Pagina, filtros.TamanoPagina));
    }

    [HttpGet("{id:long}")]
    [Permiso("stock.consultar")]
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
