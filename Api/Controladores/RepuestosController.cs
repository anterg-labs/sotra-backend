using Api.DTOs;
using Api.DTOs.MovimientosStock;
using Api.DTOs.Repuestos;
using Api.Mapeadores;
using Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

[ApiController]
[Route("api/v1/repuestos")]
public class RepuestosController : ControllerBase
{
    private readonly RepuestoDAO _repuestoDao;
    private readonly MovimientoStockDAO _movimientoStockDao;
    private readonly UsuarioActual _usuarioActual;

    public RepuestosController(RepuestoDAO repuestoDao, MovimientoStockDAO movimientoStockDao,
        UsuarioActual usuarioActual)
    {
        _repuestoDao = repuestoDao;
        _movimientoStockDao = movimientoStockDao;
        _usuarioActual = usuarioActual;
    }

    [Authorize(Policy = Politicas.StockConsultar)]
    [HttpGet]
    public ActionResult<PaginaDto<RepuestoDto>> ObtenerTodos([FromQuery] FiltrosRepuestosDto filtros)
    {
        if (!PaginaDto<RepuestoDto>.EsValida(filtros.Pagina, filtros.TamanoPagina))
        {
            return BadRequest(PaginaDto<RepuestoDto>.ErrorDePaginacion());
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

        var respuesta = new List<RepuestoDto>();
        foreach (var repuesto in _repuestoDao.ListarTodo())
        {
            var repuestoDto = RepuestoMapeador.ADto(repuesto);

            if (textoBuscado != "")
            {
                var coincide = PaginaDto<RepuestoDto>.Contiene(repuestoDto.Codigo, textoBuscado)
                               || PaginaDto<RepuestoDto>.Contiene(repuestoDto.Nombre, textoBuscado)
                               || PaginaDto<RepuestoDto>.Contiene(repuestoDto.Marca, textoBuscado)
                               || PaginaDto<RepuestoDto>.Contiene(repuestoDto.Descripcion, textoBuscado);
                if (!coincide)
                {
                    continue;
                }
            }

            if (!string.IsNullOrEmpty(filtros.Estado) && repuestoDto.Estado != filtros.Estado)
            {
                continue;
            }

            if (ubicacionBuscada != "" && !PaginaDto<RepuestoDto>.Contiene(repuestoDto.Ubicacion, ubicacionBuscada))
            {
                continue;
            }

            if (filtros.BajoStock == true && repuestoDto.Cantidad > repuestoDto.CantidadMinima)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(filtros.EstadoStock) && repuestoDto.EstadoStock != filtros.EstadoStock)
            {
                continue;
            }

            respuesta.Add(repuestoDto);
        }

        if (filtros.Orden == "criticidad")
        {
            respuesta = respuesta
                .OrderBy(repuesto => PesoDeCriticidad(repuesto.EstadoStock))
                .ThenBy(repuesto => repuesto.CantidadMinima > 0 ? (double)repuesto.Cantidad / repuesto.CantidadMinima : double.MaxValue)
                .ThenBy(repuesto => repuesto.Codigo)
                .ToList();
        }

        return Ok(PaginaDto<RepuestoDto>.Crear(respuesta, filtros.Pagina, filtros.TamanoPagina));
    }

    // Contadores para el aviso de stock insuficiente: solo repuestos activos.
    [Authorize(Policy = Politicas.StockConsultar)]
    [HttpGet("alertas-stock")]
    public ActionResult<AlertasStockDto> ObtenerAlertasStock()
    {
        var bajo = 0;
        var sinStock = 0;
        foreach (var repuesto in _repuestoDao.ListarTodo())
        {
            if (!repuesto.Estado)
            {
                continue;
            }

            var estadoStock = RepuestoMapeador.CalcularEstadoStock(repuesto.Cantidad, repuesto.CantMin ?? 0);
            if (estadoStock == RepuestoMapeador.StockBajo)
            {
                bajo++;
            }
            else if (estadoStock == RepuestoMapeador.SinStock)
            {
                sinStock++;
            }
        }

        return Ok(new AlertasStockDto { Bajo = bajo, SinStock = sinStock, Total = bajo + sinStock });
    }

    private static int PesoDeCriticidad(string estadoStock)
    {
        if (estadoStock == RepuestoMapeador.SinStock)
        {
            return 0;
        }

        return estadoStock == RepuestoMapeador.StockBajo ? 1 : 2;
    }

    [Authorize(Policy = Politicas.StockRegistrarMovimiento)]
    [HttpPost("{id:long}/ingresos")]
    public ActionResult<MovimientoStockDto> RegistrarIngreso(long id, IngresoStockDto solicitud)
    {
        var repuesto = _repuestoDao.Seleccionar(id);
        if (repuesto is null)
        {
            return NotFound();
        }

        if (!repuesto.Estado)
        {
            return Conflict(new
            {
                codigo = "REPUESTO_INACTIVO",
                mensaje = "No se puede ingresar stock a un repuesto dado de baja."
            });
        }

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

        var motivo = "";
        if (solicitud.Motivo is not null)
        {
            motivo = solicitud.Motivo.Trim();
        }

        var ubicacion = "";
        if (solicitud.Ubicacion is not null)
        {
            ubicacion = solicitud.Ubicacion.Trim();
        }

        if (motivo == "" || ubicacion == "")
        {
            return BadRequest(new
            {
                codigo = "DATOS_OBLIGATORIOS",
                mensaje = "El motivo y la ubicación del ingreso son obligatorios."
            });
        }

        string? observaciones = null;
        if (!string.IsNullOrWhiteSpace(solicitud.Observaciones))
        {
            observaciones = solicitud.Observaciones.Trim();
        }

        var fechaMovimiento = DateTime.UtcNow;
        if (solicitud.Fecha != hoy)
        {
            fechaMovimiento = solicitud.Fecha.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);
        }

        var movimiento = _movimientoStockDao.RegistrarIngreso(repuesto, solicitud.Cantidad, fechaMovimiento,
            motivo, ubicacion, observaciones, _usuarioActual.ObtenerIdUsuario());
        return CreatedAtAction(nameof(ObtenerPorId), new { id = id }, MovimientoStockMapeador.ADto(movimiento));
    }

    // Retira unidades del stock. Solo se rechaza si se pide más de lo disponible: quedar por debajo
    // del mínimo está permitido (el repuesto pasa a tener alerta de stock).
    [Authorize(Policy = Politicas.StockRegistrarMovimiento)]
    [HttpPost("{id:long}/egresos")]
    public ActionResult<MovimientoStockDto> RegistrarEgreso(long id, EgresoStockDto solicitud)
    {
        var repuesto = _repuestoDao.Seleccionar(id);
        if (repuesto is null)
        {
            return NotFound();
        }

        if (!repuesto.Estado)
        {
            return Conflict(new
            {
                codigo = "REPUESTO_INACTIVO",
                mensaje = "No se puede retirar stock de un repuesto dado de baja."
            });
        }

        if (solicitud.Cantidad <= 0)
        {
            return BadRequest(new
            {
                codigo = "CANTIDAD_INVALIDA",
                mensaje = "La cantidad a retirar debe ser un número entero mayor a cero."
            });
        }

        var motivo = solicitud.Motivo?.Trim() ?? "";
        if (motivo == "")
        {
            return BadRequest(new { codigo = "DATOS_OBLIGATORIOS", mensaje = "Indicá el motivo del egreso." });
        }

        if (solicitud.Cantidad > repuesto.Cantidad)
        {
            return Conflict(new
            {
                codigo = "STOCK_INSUFICIENTE",
                mensaje = "No hay stock suficiente: disponible " + repuesto.Cantidad + "."
            });
        }

        var ubicacion = string.IsNullOrWhiteSpace(solicitud.Ubicacion) ? repuesto.Ubicacion ?? "" : solicitud.Ubicacion.Trim();
        var observaciones = string.IsNullOrWhiteSpace(solicitud.Observaciones) ? null : solicitud.Observaciones.Trim();
        var movimiento = _movimientoStockDao.RegistrarEgreso(repuesto, solicitud.Cantidad, DateTime.UtcNow, motivo,
            ubicacion, observaciones, _usuarioActual.ObtenerIdUsuario());
        return CreatedAtAction(nameof(ObtenerPorId), new { id = id }, MovimientoStockMapeador.ADto(movimiento));
    }

    // Corrige el stock con el conteo real del depósito.
    [Authorize(Policy = Politicas.StockRegistrarMovimiento)]
    [HttpPost("{id:long}/ajustes")]
    public ActionResult<MovimientoStockDto> RegistrarAjuste(long id, AjusteStockDto solicitud)
    {
        var repuesto = _repuestoDao.Seleccionar(id);
        if (repuesto is null)
        {
            return NotFound();
        }

        if (!repuesto.Estado)
        {
            return Conflict(new
            {
                codigo = "REPUESTO_INACTIVO",
                mensaje = "No se puede ajustar el stock de un repuesto dado de baja."
            });
        }

        if (solicitud.CantidadReal < 0)
        {
            return BadRequest(new
            {
                codigo = "CANTIDAD_INVALIDA",
                mensaje = "El stock real debe ser un número entero mayor o igual a cero."
            });
        }

        var motivo = solicitud.Motivo?.Trim() ?? "";
        if (motivo == "")
        {
            return BadRequest(new { codigo = "DATOS_OBLIGATORIOS", mensaje = "Indicá el motivo del ajuste." });
        }

        if (solicitud.CantidadReal == repuesto.Cantidad)
        {
            return BadRequest(new
            {
                codigo = "SIN_DIFERENCIA",
                mensaje = "El stock real es igual al registrado: no hay nada que ajustar."
            });
        }

        var observaciones = string.IsNullOrWhiteSpace(solicitud.Observaciones) ? null : solicitud.Observaciones.Trim();
        var movimiento = _movimientoStockDao.RegistrarAjuste(repuesto, solicitud.CantidadReal, DateTime.UtcNow, motivo,
            repuesto.Ubicacion ?? "", observaciones, _usuarioActual.ObtenerIdUsuario());
        return CreatedAtAction(nameof(ObtenerPorId), new { id = id }, MovimientoStockMapeador.ADto(movimiento));
    }

    [Authorize(Policy = Politicas.StockConsultar)]
    [HttpGet("{id:long}")]
    public ActionResult<RepuestoDto> ObtenerPorId(long id)
    {
        var repuesto = _repuestoDao.Seleccionar(id);
        if (repuesto is null)
        {
            return NotFound();
        }

        return Ok(RepuestoMapeador.ADto(repuesto));
    }

    [Authorize(Policy = Politicas.RepuestoGestionar)]
    [HttpPost]
    public ActionResult<RepuestoDto> Crear(RepuestoCrearDto solicitud)
    {
        try
        {
            var repuesto = _repuestoDao.Insertar(RepuestoMapeador.AEntidad(solicitud));
            var respuesta = RepuestoMapeador.ADto(repuesto);
            return CreatedAtAction(nameof(ObtenerPorId), new { id = repuesto.IdRepuesto }, respuesta);
        }
        catch (DbUpdateException excepcion) when (excepcion.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict(new { mensaje = "Ya existe un repuesto con ese código." });
        }
    }

    [Authorize(Policy = Politicas.RepuestoGestionar)]
    [HttpPut("{id:long}")]
    public ActionResult<RepuestoDto> Actualizar(long id, RepuestoActualizarDto solicitud)
    {
        try
        {
            var repuesto = _repuestoDao.Seleccionar(id);
            if (repuesto is null)
            {
                return NotFound();
            }

            RepuestoMapeador.AplicarCambios(repuesto, solicitud);
            _repuestoDao.Actualizar(repuesto);
            return Ok(RepuestoMapeador.ADto(repuesto));
        }
        catch (DbUpdateException excepcion) when (excepcion.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict(new { mensaje = "Ya existe un repuesto con ese código." });
        }
    }

    [Authorize(Policy = Politicas.RepuestoGestionar)]
    [HttpPatch("{id:long}")]
    public ActionResult<RepuestoDto> Desactivar(long id, RepuestoEstadoDto solicitud)
    {
        if (!string.Equals(solicitud.Estado, "inactivo", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { mensaje = "Este endpoint solo acepta el estado 'inactivo'." });
        }

        if (!_repuestoDao.Desactivar(id))
        {
            return NotFound();
        }

        var repuesto = _repuestoDao.Seleccionar(id)!;
        return Ok(RepuestoMapeador.ADto(repuesto));
    }
}
