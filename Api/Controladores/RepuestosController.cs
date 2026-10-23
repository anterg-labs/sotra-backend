using Api.Infraestructura;
using Api.DTOs;
using Api.DTOs.MovimientosStock;
using Api.DTOs.Repuestos;
using Api.Mapeadores;
using Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

[ApiController]
[Route("api/v1/repuestos")]
public class RepuestosController : ControllerBase
{
    private readonly RepuestoDAO _repuestoDao;
    private readonly MovimientoStockDAO _movimientoStockDao;
    private readonly UsuarioActual _usuarioActual;

    public RepuestosController(RepuestoDAO repuestoDao, MovimientoStockDAO movimientoStockDao, UsuarioActual usuarioActual)
    {
        _repuestoDao = repuestoDao;
        _movimientoStockDao = movimientoStockDao;
        _usuarioActual = usuarioActual;
    }

    [HttpGet]
    [Authorize(Policy = Politicas.StockConsultar)]
    public async Task<ActionResult<PaginaDto<RepuestoDto>>> ObtenerTodos([FromQuery] FiltrosRepuestosDto filtros, CancellationToken cancelacion)
    {
        if (!PaginaDto<RepuestoDto>.EsValida(filtros.Pagina, filtros.TamanoPagina))
        {
            return BadRequest(PaginaDto<RepuestoDto>.ErrorDePaginacion());
        }

        if (!RepuestoDAO.ColumnasOrdenables.Contiene(filtros.OrdenarPor))
        {
            return this.OrdenInvalido(RepuestoDAO.ColumnasOrdenables.Claves);
        }

        if (!ConsultasDeTexto.TryTraducirEstado(filtros.Estado, out var estado))
        {
            return Ok(PaginaDto<RepuestoDto>.Crear([], 0, filtros.Pagina, filtros.TamanoPagina));
        }

        if (filtros.EstadoStock is not null && !EstadosStock.Validos.Contains(filtros.EstadoStock))
        {
            return BadRequest(new
            {
                codigo = "ESTADO_STOCK_INVALIDO",
                mensaje = "El estado de stock debe ser normal, bajo o sin_stock."
            });
        }

        var filtro = new FiltroRepuestos(filtros.Buscar, estado, filtros.Ubicacion, filtros.BajoStock,
            filtros.EstadoStock, string.Equals(filtros.Orden, "criticidad", StringComparison.OrdinalIgnoreCase),
            filtros.OrdenarPor, filtros.DireccionOrden == "desc");
        var (entidades, total) = await _repuestoDao.ListarPaginaAsync(filtro, filtros.Pagina, filtros.TamanoPagina, cancelacion);
        var elementos = entidades.Select(RepuestoMapeador.ADto).ToList();
        return Ok(PaginaDto<RepuestoDto>.Crear(elementos, total, filtros.Pagina, filtros.TamanoPagina));
    }

    [HttpGet("alertas-stock")]
    [Authorize(Policy = Politicas.StockConsultar)]
    public async Task<ActionResult<AlertasStockDto>> ObtenerAlertasStock(CancellationToken cancelacion)
    {
        var alertas = await _repuestoDao.ContarAlertasAsync(cancelacion);
        return Ok(new AlertasStockDto { Bajo = alertas.Bajo, SinStock = alertas.SinStock, Total = alertas.Total });
    }

    [HttpPost("{id:long}/ingresos")]
    [Authorize(Policy = Politicas.StockRegistrarMovimiento)]
    public async Task<ActionResult<MovimientoStockDto>> RegistrarIngreso(
        long id, IngresoStockDto solicitud, CancellationToken cancelacion)
    {
        if (solicitud.Cantidad <= 0)
        {
            return BadRequest(new
            {
                codigo = "CANTIDAD_INVALIDA",
                mensaje = "La cantidad a ingresar debe ser mayor a cero."
            });
        }

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        if (solicitud.Fecha == default || solicitud.Fecha > hoy.AddDays(1))
        {
            return BadRequest(new
            {
                codigo = "FECHA_INVALIDA",
                mensaje = "Indicá una fecha de ingreso válida que no sea futura."
            });
        }

        var motivo = solicitud.Motivo?.Trim() ?? "";
        var ubicacion = solicitud.Ubicacion?.Trim() ?? "";
        if (motivo == "" || ubicacion == "")
        {
            return BadRequest(new
            {
                codigo = "DATOS_OBLIGATORIOS",
                mensaje = "El motivo y la ubicación del ingreso son obligatorios."
            });
        }

        var fechaMovimiento = DateTime.UtcNow;
        if (solicitud.Fecha != hoy)
        {
            fechaMovimiento = solicitud.Fecha.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);
        }

        return await RegistrarMovimiento(new SolicitudMovimientoStock(id, TiposMovimientoStock.Ingreso, solicitud.Cantidad,
            fechaMovimiento, motivo, ubicacion, ObservacionesONulo(solicitud.Observaciones)), cancelacion);
    }

    [HttpPost("{id:long}/egresos")]
    [Authorize(Policy = Politicas.StockRegistrarMovimiento)]
    public async Task<ActionResult<MovimientoStockDto>> RegistrarEgreso(
        long id, EgresoStockDto solicitud, CancellationToken cancelacion)
    {
        if (solicitud.Cantidad <= 0)
        {
            return BadRequest(new
            {
                codigo = "CANTIDAD_INVALIDA",
                mensaje = "La cantidad a retirar debe ser mayor a cero."
            });
        }

        var motivo = solicitud.Motivo?.Trim() ?? "";
        if (motivo == "")
        {
            return BadRequest(new
            {
                codigo = "DATOS_OBLIGATORIOS",
                mensaje = "El motivo del egreso es obligatorio."
            });
        }

        return await RegistrarMovimiento(new SolicitudMovimientoStock(id, TiposMovimientoStock.Egreso, solicitud.Cantidad,
            DateTime.UtcNow, motivo, solicitud.Ubicacion?.Trim(), ObservacionesONulo(solicitud.Observaciones)), cancelacion);
    }

    [HttpPost("{id:long}/ajustes")]
    [Authorize(Policy = Politicas.StockRegistrarMovimiento)]
    public async Task<ActionResult<MovimientoStockDto>> RegistrarAjuste(
        long id, AjusteStockDto solicitud, CancellationToken cancelacion)
    {
        if (solicitud.CantidadReal < 0)
        {
            return BadRequest(new
            {
                codigo = "CANTIDAD_INVALIDA",
                mensaje = "El stock real no puede ser negativo."
            });
        }

        var motivo = solicitud.Motivo?.Trim() ?? "";
        if (motivo == "")
        {
            return BadRequest(new
            {
                codigo = "DATOS_OBLIGATORIOS",
                mensaje = "El motivo del ajuste es obligatorio."
            });
        }

        return await RegistrarMovimiento(new SolicitudMovimientoStock(id, TiposMovimientoStock.Ajuste, solicitud.CantidadReal,
            DateTime.UtcNow, motivo, null, ObservacionesONulo(solicitud.Observaciones)), cancelacion);
    }

    [HttpPatch("{id:long}/stock-minimo")]
    [Authorize(Policy = Politicas.RepuestoGestionar)]
    public async Task<ActionResult<RepuestoDto>> ActualizarStockMinimo(long id, StockMinimoDto solicitud, CancellationToken cancelacion)
    {
        var repuesto = await _repuestoDao.ActualizarMinimoAsync(id, solicitud.CantidadMinima, cancelacion);
        if (repuesto is null)
        {
            return NotFound();
        }

        return Ok(RepuestoMapeador.ADto(repuesto));
    }

    private async Task<ActionResult<MovimientoStockDto>> RegistrarMovimiento(SolicitudMovimientoStock solicitud, CancellationToken cancelacion)
    {
        // Cada movimiento queda registrado con el usuario que lo hizo (null sin token).
        solicitud = solicitud with { IdUsuario = await _usuarioActual.ObtenerIdUsuarioAsync(cancelacion) };
        var resultado = await _movimientoStockDao.RegistrarAsync(solicitud, cancelacion);
        return resultado.Estado switch
        {
            EstadoMovimientoStock.Registrado => CreatedAtAction(nameof(ObtenerPorId), new { id = solicitud.RepuestoId },
                MovimientoStockMapeador.ADto(resultado.Movimiento!)),
            EstadoMovimientoStock.RepuestoNoEncontrado => NotFound(),
            EstadoMovimientoStock.RepuestoInactivo => Conflict(new
            {
                codigo = "REPUESTO_INACTIVO",
                mensaje = "No se puede mover el stock de un repuesto dado de baja."
            }),
            EstadoMovimientoStock.StockInsuficiente => Conflict(new
            {
                codigo = "STOCK_INSUFICIENTE",
                mensaje = "No hay stock suficiente para retirar esa cantidad."
            }),
            _ => Conflict(new
            {
                codigo = "STOCK_EXCEDIDO",
                mensaje = "La cantidad resultante supera el máximo permitido."
            }),
        };
    }

    private static string? ObservacionesONulo(string? observaciones)
    {
        return string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim();
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = Politicas.StockConsultar)]
    public async Task<ActionResult<RepuestoDto>> ObtenerPorId(long id, CancellationToken cancelacion)
    {
        var repuesto = await _repuestoDao.SeleccionarAsync(id, cancelacion);
        if (repuesto is null)
        {
            return NotFound();
        }

        return Ok(RepuestoMapeador.ADto(repuesto));
    }

    [HttpPost]
    [Authorize(Policy = Politicas.RepuestoGestionar)]
    public async Task<ActionResult<RepuestoDto>> Crear(RepuestoCrearDto solicitud, CancellationToken cancelacion)
    {
        var repuesto = await _repuestoDao.InsertarAsync(RepuestoMapeador.AEntidad(solicitud), cancelacion);
        var respuesta = RepuestoMapeador.ADto(repuesto);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = repuesto.IdRepuesto }, respuesta);
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = Politicas.RepuestoGestionar)]
    public async Task<ActionResult<RepuestoDto>> Actualizar(long id, RepuestoActualizarDto solicitud, CancellationToken cancelacion)
    {
        var repuesto = await _repuestoDao.SeleccionarAsync(id, cancelacion);
        if (repuesto is null)
        {
            return NotFound();
        }

        RepuestoMapeador.AplicarCambios(repuesto, solicitud);
        await _repuestoDao.ActualizarAsync(repuesto, cancelacion);
        return Ok(RepuestoMapeador.ADto(repuesto));
    }

    [HttpPatch("{id:long}")]
    [Authorize(Policy = Politicas.RepuestoGestionar)]
    public async Task<ActionResult<RepuestoDto>> CambiarEstado(long id, RepuestoEstadoDto solicitud, CancellationToken cancelacion)
    {
        bool encontrado;
        if (string.Equals(solicitud.Estado, "activo", StringComparison.OrdinalIgnoreCase))
        {
            encontrado = await _repuestoDao.ReactivarAsync(id, cancelacion);
        }
        else if (string.Equals(solicitud.Estado, "inactivo", StringComparison.OrdinalIgnoreCase))
        {
            encontrado = await _repuestoDao.DesactivarAsync(id, cancelacion);
        }
        else
        {
            return BadRequest(new { mensaje = "El estado debe ser 'activo' o 'inactivo'." });
        }

        if (!encontrado)
        {
            return NotFound();
        }

        return Ok(RepuestoMapeador.ADto((await _repuestoDao.SeleccionarAsync(id, cancelacion))!));
    }
}
