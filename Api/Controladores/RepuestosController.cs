using Api.Infraestructura;
using Api.DTOs;
using Api.DTOs.MovimientosStock;
using Api.DTOs.Repuestos;
using Api.Mapeadores;
using Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SOTRA_BACKEND.Modelos;
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

    /// <summary>Lista los repuestos con filtros por texto, ubicación, estado y alerta de stock.</summary>
    [HttpGet]
    [Authorize(Policy = Politicas.StockConsultar)]
    public async Task<ActionResult<PaginaDto<RepuestoDto>>> ObtenerTodos([FromQuery] FiltrosRepuestosDto filtros, CancellationToken cancelacion)
    {
        if (!PaginaDto<RepuestoDto>.EsValida(filtros.Pagina, filtros.TamanoPagina))
        {
            return this.PaginacionInvalida();
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
            return this.RespuestaDeError(400, "ESTADO_STOCK_INVALIDO", "El estado de stock debe ser normal, bajo o sin_stock.");
        }

        var filtro = new FiltroRepuestos(filtros.Buscar, estado, filtros.Ubicacion, filtros.BajoStock,
            filtros.EstadoStock, string.Equals(filtros.Orden, "criticidad", StringComparison.OrdinalIgnoreCase),
            filtros.OrdenarPor, filtros.DireccionOrden == "desc", filtros.Categoria);
        var (entidades, total) = await _repuestoDao.ListarPaginaAsync(filtro, filtros.Pagina, filtros.TamanoPagina, cancelacion);
        var comprometido = await _repuestoDao.ObtenerComprometidoAsync(entidades.Select(r => r.IdRepuesto).ToList(), cancelacion);
        var elementos = entidades.Select(r => RepuestoMapeador.ADto(r, comprometido.GetValueOrDefault(r.IdRepuesto))).ToList();
        return Ok(PaginaDto<RepuestoDto>.Crear(elementos, total, filtros.Pagina, filtros.TamanoPagina));
    }

    /// <summary>Repuestos sin stock o con stock bajo.</summary>
    [HttpGet("alertas-stock")]
    [Authorize(Policy = Politicas.StockConsultar)]
    public async Task<ActionResult<AlertasStockDto>> ObtenerAlertasStock(CancellationToken cancelacion)
    {
        var alertas = await _repuestoDao.ContarAlertasAsync(cancelacion);
        return Ok(new AlertasStockDto { Bajo = alertas.Bajo, SinStock = alertas.SinStock, Total = alertas.Total });
    }

    /// <summary>Registra un ingreso de stock.</summary>
    [HttpPost("{id:long}/ingresos")]
    [Authorize(Policy = Politicas.StockRegistrarMovimiento)]
    public async Task<ActionResult<MovimientoStockDto>> RegistrarIngreso(
        long id, IngresoStockDto solicitud, CancellationToken cancelacion)
    {
        if (solicitud.Cantidad <= 0)
        {
            return this.RespuestaDeError(400, "CANTIDAD_INVALIDA", "La cantidad a ingresar debe ser mayor a cero.");
        }

        return await RegistrarMovimiento(new SolicitudMovimientoStock(id, TiposMovimientoStock.Ingreso, solicitud.Cantidad,
            DateTime.UtcNow, null, null, ObservacionesONulo(solicitud.Observaciones)), cancelacion);
    }

    /// <summary>Registra un egreso de stock (no se usa para repuestos de órdenes).</summary>
    [HttpPost("{id:long}/egresos")]
    [Authorize(Policy = Politicas.StockRegistrarMovimiento)]
    public async Task<ActionResult<MovimientoStockDto>> RegistrarEgreso(
        long id, EgresoStockDto solicitud, CancellationToken cancelacion)
    {
        if (solicitud.Cantidad <= 0)
        {
            return this.RespuestaDeError(400, "CANTIDAD_INVALIDA", "La cantidad a retirar debe ser mayor a cero.");
        }

        var motivo = solicitud.Motivo?.Trim() ?? "";
        if (motivo == "")
        {
            return this.RespuestaDeError(400, "DATOS_OBLIGATORIOS", "El motivo del egreso es obligatorio.");
        }

        return await RegistrarMovimiento(new SolicitudMovimientoStock(id, TiposMovimientoStock.Egreso, solicitud.Cantidad,
            DateTime.UtcNow, motivo, solicitud.Ubicacion?.Trim(), ObservacionesONulo(solicitud.Observaciones)), cancelacion);
    }

    /// <summary>Registra un ajuste de stock con su motivo.</summary>
    [HttpPost("{id:long}/ajustes")]
    [Authorize(Policy = Politicas.StockRegistrarMovimiento)]
    public async Task<ActionResult<MovimientoStockDto>> RegistrarAjuste(
        long id, AjusteStockDto solicitud, CancellationToken cancelacion)
    {
        if (solicitud.CantidadReal < 0)
        {
            return this.RespuestaDeError(400, "CANTIDAD_INVALIDA", "El stock real no puede ser negativo.");
        }

        var motivo = solicitud.Motivo?.Trim() ?? "";
        if (motivo == "")
        {
            return this.RespuestaDeError(400, "DATOS_OBLIGATORIOS", "El motivo del ajuste es obligatorio.");
        }

        return await RegistrarMovimiento(new SolicitudMovimientoStock(id, TiposMovimientoStock.Ajuste, solicitud.CantidadReal,
            DateTime.UtcNow, motivo, null, ObservacionesONulo(solicitud.Observaciones)), cancelacion);
    }

    /// <summary>Cambia el stock mínimo de un repuesto.</summary>
    [HttpPatch("{id:long}/stock-minimo")]
    [Authorize(Policy = Politicas.RepuestoGestionar)]
    public async Task<ActionResult<RepuestoDto>> ActualizarStockMinimo(long id, StockMinimoDto solicitud, CancellationToken cancelacion)
    {
        var repuesto = await _repuestoDao.ActualizarMinimoAsync(id, solicitud.CantidadMinima, cancelacion);
        if (repuesto is null)
        {
            return NotFound();
        }

        return Ok(await ADtoAsync(repuesto, cancelacion));
    }

    private async Task<RepuestoDto> ADtoAsync(Repuesto repuesto, CancellationToken cancelacion)
    {
        var comprometido = await _repuestoDao.ObtenerComprometidoAsync([repuesto.IdRepuesto], cancelacion);
        return RepuestoMapeador.ADto(repuesto, comprometido.GetValueOrDefault(repuesto.IdRepuesto));
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
            EstadoMovimientoStock.RepuestoInactivo => this.RespuestaDeError(409, "REPUESTO_INACTIVO", "No se puede mover el stock de un repuesto dado de baja."),
            EstadoMovimientoStock.StockInsuficiente => this.RespuestaDeError(409, "STOCK_INSUFICIENTE", "No hay stock suficiente para retirar esa cantidad."),
            EstadoMovimientoStock.Comprometido => this.RespuestaDeError(409, "STOCK_COMPROMETIDO",
                $"No se puede dejar el stock por debajo de {resultado.Comprometido!.Total}: hay {resultado.Comprometido.Reservado} unidades reservadas y {resultado.Comprometido.Entregado} entregadas a órdenes abiertas."),
            _ => this.RespuestaDeError(409, "STOCK_EXCEDIDO", "La cantidad resultante supera el máximo permitido."),
        };
    }

    private static string? ObservacionesONulo(string? observaciones)
    {
        return string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim();
    }

    /// <summary>Devuelve un repuesto por su id, con su stock reservado y entregado.</summary>
    [HttpGet("{id:long}")]
    [Authorize(Policy = Politicas.StockConsultar)]
    public async Task<ActionResult<RepuestoDto>> ObtenerPorId(long id, CancellationToken cancelacion)
    {
        var repuesto = await _repuestoDao.SeleccionarAsync(id, cancelacion);
        if (repuesto is null)
        {
            return NotFound();
        }

        return Ok(await ADtoAsync(repuesto, cancelacion));
    }

    /// <summary>Registra un repuesto.</summary>
    [HttpPost]
    [Authorize(Policy = Politicas.RepuestoGestionar)]
    public async Task<ActionResult<RepuestoDto>> Crear(RepuestoCrearDto solicitud, CancellationToken cancelacion)
    {
        var repuesto = await _repuestoDao.InsertarAsync(RepuestoMapeador.AEntidad(solicitud), cancelacion);
        var respuesta = await ADtoAsync(repuesto, cancelacion);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = repuesto.IdRepuesto }, respuesta);
    }

    /// <summary>Modifica los datos de un repuesto.</summary>
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
        return Ok(await ADtoAsync(repuesto, cancelacion));
    }

    /// <summary>Da de baja o reactiva un repuesto.</summary>
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
            return this.RespuestaDeError(400, "ESTADO_INVALIDO", "El estado debe ser 'activo' o 'inactivo'.");
        }

        if (!encontrado)
        {
            return NotFound();
        }

        return Ok(await ADtoAsync((await _repuestoDao.SeleccionarAsync(id, cancelacion))!, cancelacion));
    }
}
