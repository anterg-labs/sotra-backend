using Api.DTOs;
using Api.DTOs.MovimientosStock;
using Api.DTOs.Repuestos;
using Api.Mapeadores;
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

    public RepuestosController(RepuestoDAO repuestoDao, MovimientoStockDAO movimientoStockDao)
    {
        _repuestoDao = repuestoDao;
        _movimientoStockDao = movimientoStockDao;
    }

    [HttpGet]
    public ActionResult<PaginaDto<RepuestoDto>> ObtenerTodos([FromQuery] FiltrosRepuestosDto filtros)
    {
        if (!PaginaDto<RepuestoDto>.EsValida(filtros.Pagina, filtros.TamanoPagina))
        {
            return BadRequest(PaginaDto<RepuestoDto>.ErrorDePaginacion());
        }

        if (!ConsultasDeTexto.TryTraducirEstado(filtros.Estado, out var estado))
        {
            return Ok(PaginaDto<RepuestoDto>.Crear([], 0, filtros.Pagina, filtros.TamanoPagina));
        }

        var filtro = new FiltroRepuestos(filtros.Buscar, estado, filtros.Ubicacion, filtros.BajoStock);
        var (entidades, total) = _repuestoDao.ListarPagina(filtro, filtros.Pagina, filtros.TamanoPagina);
        var elementos = entidades.Select(RepuestoMapeador.ADto).ToList();
        return Ok(PaginaDto<RepuestoDto>.Crear(elementos, total, filtros.Pagina, filtros.TamanoPagina));
    }

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
            motivo, ubicacion, observaciones);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = id }, MovimientoStockMapeador.ADto(movimiento));
    }

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
