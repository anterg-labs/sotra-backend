using Api.Infraestructura;
using Microsoft.AspNetCore.RateLimiting;
using Api.DTOs;
using Api.DTOs.OrdenesTrabajo;
using Api.Mapeadores;
using Api.Seguridad;
using Api.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SOTRA_BACKEND.Modelos;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

[ApiController]
[Route("api/v1/ordenes-trabajo")]
public class OrdenesTrabajoController : ControllerBase
{
    private readonly OrdenTrabajoDAO _ordenTrabajoDao;
    private readonly ServicioSugerencias _servicioSugerencias;
    private readonly UsuarioActual _usuarioActual;
    private const int StatusCodeClienteCerroLaSolicitud = 499;

    private readonly ILogger<OrdenesTrabajoController> _logger;

    public OrdenesTrabajoController(OrdenTrabajoDAO ordenTrabajoDao,
        ServicioSugerencias servicioSugerencias, ILogger<OrdenesTrabajoController> logger,
        UsuarioActual usuarioActual)
    {
        _ordenTrabajoDao = ordenTrabajoDao;
        _servicioSugerencias = servicioSugerencias;
        _usuarioActual = usuarioActual;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = Politicas.OtConsultar)]
    public async Task<ActionResult<PaginaDto<OrdenTrabajoDto>>> ObtenerTodos([FromQuery] FiltrosOrdenesTrabajoDto filtros, CancellationToken cancelacion)
    {
        if (!PaginaDto<OrdenTrabajoDto>.EsValida(filtros.Pagina, filtros.TamanoPagina))
        {
            return this.RespuestaDeError(400, "PAGINACION_INVALIDA", "La página debe ser 1 o mayor y el tamaño de página debe estar entre 1 y "
                          + PaginaDto<OrdenTrabajoDto>.TamanoPaginaMaximo + ".");
        }

        if (filtros.FechaDesde is not null && filtros.FechaHasta is not null
            && filtros.FechaDesde > filtros.FechaHasta)
        {
            return this.RespuestaDeError(400, "RANGO_FECHAS_INVALIDO", "La fecha desde no puede ser posterior a la fecha hasta.");
        }

        if (!OrdenTrabajoDAO.ColumnasOrdenables.Contiene(filtros.OrdenarPor))
        {
            return this.OrdenInvalido(OrdenTrabajoDAO.ColumnasOrdenables.Claves);
        }

        // El técnico solo ve las órdenes que tiene asignadas, aunque pida otro filtro.
        var tecnicoId = filtros.TecnicoId;
        if (User.EsSoloTecnico())
        {
            var idUsuario = await _usuarioActual.ObtenerIdUsuarioAsync(cancelacion);
            if (idUsuario is null || (tecnicoId is not null && tecnicoId != idUsuario))
            {
                return Ok(PaginaDto<OrdenTrabajoDto>.Crear([], 0, filtros.Pagina, filtros.TamanoPagina));
            }

            tecnicoId = idUsuario;
        }

        var filtro = new FiltroOrdenesTrabajo(
            filtros.Buscar, filtros.ClienteId, filtros.ActivoId, tecnicoId, filtros.Estado,
            filtros.Prioridad, filtros.Tipo is null or "" ? null : filtros.Tipo == "preventivo",
            filtros.FechaDesde, filtros.FechaHasta, filtros.ConSugerencias,
            OrdenarPorCreacion: false, Descendente: filtros.DireccionOrden == "desc", OrdenarPor: filtros.OrdenarPor, Fase: filtros.Fase);

        if (filtros.Tipo is not (null or "" or "preventivo" or "correctivo"))
        {
            return Ok(PaginaDto<OrdenTrabajoDto>.Crear([], 0, filtros.Pagina, filtros.TamanoPagina));
        }

        var (ordenes, total) = await _ordenTrabajoDao.ListarPaginaAsync(filtro, filtros.Pagina, filtros.TamanoPagina, cancelacion);
        var reglas = await _ordenTrabajoDao.ObtenerReglasAsync(cancelacion);
        var elementos = ordenes.Select(orden => OrdenTrabajoMapeador.ADto(orden, reglas, User.EsSoloTecnico())).ToList();
        return Ok(PaginaDto<OrdenTrabajoDto>.Crear(elementos, total, filtros.Pagina, filtros.TamanoPagina));
    }

    [HttpGet("estados")]
    [Authorize(Policy = Politicas.OtConsultar)]
    [ProducesResponseType<List<EstadoOrdenTrabajoDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EstadoOrdenTrabajoDto>>> ObtenerEstados(CancellationToken cancelacion)
    {
        var reglas = await _ordenTrabajoDao.ObtenerReglasAsync(cancelacion);
        return Ok(reglas.Estados
            .Select(estado => new EstadoOrdenTrabajoDto(estado.Nombre, estado.Etiqueta, estado.Orden, estado.Fase, estado.Tono))
            .ToList());
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = Politicas.OtConsultar)]
    public async Task<ActionResult<OrdenTrabajoDto>> ObtenerPorId(long id, CancellationToken cancelacion)
    {
        var orden = await _ordenTrabajoDao.SeleccionarAsync(id, cancelacion);
        if (orden is null)
        {
            return NotFound();
        }

        if (!await User.PuedeVerOrdenAsync(orden, _usuarioActual, cancelacion))
        {
            return Forbid();
        }

        return OkConVersion(OrdenTrabajoMapeador.ADto(orden, await _ordenTrabajoDao.ObtenerReglasAsync(cancelacion), User.EsSoloTecnico()));
    }

    [HttpPost]
    [Authorize(Policy = Politicas.OtCrear)]
    public async Task<ActionResult<OrdenTrabajoDto>> Crear(OrdenTrabajoCrearDto solicitud, CancellationToken cancelacion)
    {
        ValidarFecha(solicitud.FechaPrevistaEjecucion);
        var orden = new OrdenTrabajo
        {
            IdCliente = solicitud.ClienteId,
            IdActivo = solicitud.ActivoId,
            IdUsuarioTecnico = solicitud.TecnicoId,
            Tipo = OrdenTrabajoMapeador.ATipoBooleano(solicitud.Tipo),
            Prioridad = OrdenTrabajoMapeador.APrioridadNumero(solicitud.Prioridad),
            Descripcion = solicitud.Descripcion.Trim(),
            Resultado = TextoOpcional.Normalizar(solicitud.Observaciones)
        };

        var ordenCreada = await _ordenTrabajoDao.CrearAsync(orden, solicitud.FechaPrevistaEjecucion,
            APedidos(solicitud.Repuestos), solicitud.Sugerencias, solicitud.Sesion,
            await _usuarioActual.ObtenerIdUsuarioAsync(cancelacion), cancelacion);
        var respuesta = OrdenTrabajoMapeador.ADto(ordenCreada, await _ordenTrabajoDao.ObtenerReglasAsync(cancelacion));
        return CreatedAtAction(nameof(ObtenerPorId), new { id = ordenCreada.IdOrden }, respuesta);
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = Politicas.OtEditar)]
    public async Task<ActionResult<OrdenTrabajoDto>> Actualizar(long id, OrdenTrabajoActualizarDto solicitud, CancellationToken cancelacion)
    {
        ValidarFecha(solicitud.FechaPrevistaEjecucion);
        var cambios = new CambiosDeOrden(
            solicitud.ClienteId, solicitud.ActivoId, solicitud.TecnicoId,
            OrdenTrabajoMapeador.ATipoBooleano(solicitud.Tipo),
            OrdenTrabajoMapeador.APrioridadNumero(solicitud.Prioridad),
            solicitud.Descripcion.Trim(), TextoOpcional.Normalizar(solicitud.Observaciones));

        var reglas = await _ordenTrabajoDao.ObtenerReglasAsync(cancelacion);
        var versionEsperada = LeerVersionEsperada();
        Func<OrdenTrabajo, bool>? coincideConVersion = versionEsperada is null
            ? null
            : orden => versionEsperada == "*"
                || versionEsperada == OrdenTrabajoMapeador.ADto(orden, reglas).Version;

        var ordenActualizada = await _ordenTrabajoDao.ActualizarAsync(id, cambios,
            solicitud.FechaPrevistaEjecucion, APedidos(solicitud.Repuestos),
            solicitud.Sugerencias, solicitud.Estado, solicitud.MotivoEstado, solicitud.Sesion,
            coincideConVersion, await _usuarioActual.ObtenerIdUsuarioAsync(cancelacion), cancelacion);
        if (ordenActualizada is null)
        {
            return NotFound();
        }

        return OkConVersion(OrdenTrabajoMapeador.ADto(ordenActualizada, reglas));
    }

    [HttpPut("{id:long}/repuestos")]
    [Authorize(Policy = Politicas.OtGestionarRepuestos)]
    [ProducesResponseType<OrdenTrabajoDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OrdenTrabajoDto>> ActualizarRepuestos(long id, OrdenRepuestosActualizarDto solicitud, CancellationToken cancelacion)
    {
        var orden = await _ordenTrabajoDao.ActualizarRepuestosAsync(id, APedidos(solicitud.Repuestos),
            await _usuarioActual.ObtenerIdUsuarioAsync(cancelacion), cancelacion);
        if (orden is null)
        {
            return NotFound();
        }

        return OkConVersion(OrdenTrabajoMapeador.ADto(orden, await _ordenTrabajoDao.ObtenerReglasAsync(cancelacion)));
    }

    private static List<RepuestoPedido> APedidos(IEnumerable<RepuestoPedidoDto> repuestos) =>
        repuestos.Select(repuesto => new RepuestoPedido(repuesto.RepuestoId, repuesto.Cantidad)).ToList();

    [HttpPatch("{id:long}/estado")]
    [Authorize(Policy = Politicas.OtRegistrarAvance)]
    public async Task<ActionResult<OrdenTrabajoDto>> CambiarEstado(long id, CambioEstadoOrdenTrabajoDto solicitud, CancellationToken cancelacion)
    {
        var orden = await _ordenTrabajoDao.SeleccionarAsync(id, cancelacion);
        if (orden is null)
        {
            return NotFound();
        }

        if (!await User.PuedeVerOrdenAsync(orden, _usuarioActual, cancelacion))
        {
            return Forbid();
        }

        var motivo = solicitud.Motivo;
        if (motivo is not null)
        {
            motivo = motivo.Trim();
        }

        var ordenActualizada = await _ordenTrabajoDao.CambiarEstadoAsync(orden, solicitud.Estado, motivo, User.EsSoloTecnico(),
            await _usuarioActual.ObtenerIdUsuarioAsync(cancelacion), cancelacion);
        return Ok(OrdenTrabajoMapeador.ADto(ordenActualizada, await _ordenTrabajoDao.ObtenerReglasAsync(cancelacion), User.EsSoloTecnico()));
    }

    [HttpPatch("{id:long}/tecnico")]
    [Authorize(Policy = Politicas.OtAsignarTecnico)]
    public async Task<ActionResult<OrdenTrabajoDto>> AsignarTecnico(long id, AsignacionTecnicoDto solicitud, CancellationToken cancelacion)
    {
        var orden = await _ordenTrabajoDao.SeleccionarAsync(id, cancelacion);
        if (orden is null)
        {
            return NotFound();
        }

        var ordenActualizada = await _ordenTrabajoDao.AsignarTecnicoAsync(orden, solicitud.TecnicoId,
            await _usuarioActual.ObtenerIdUsuarioAsync(cancelacion), cancelacion);
        return Ok(OrdenTrabajoMapeador.ADto(ordenActualizada, await _ordenTrabajoDao.ObtenerReglasAsync(cancelacion)));
    }

    private ActionResult<OrdenTrabajoDto> OkConVersion(OrdenTrabajoDto orden)
    {
        Response.Headers.ETag = "\"" + orden.Version + "\"";
        return Ok(orden);
    }

    /// <summary>Valor de <c>If-Match</c> sin comillas, o <c>null</c> si la persona no lo envió.</summary>
    private string? LeerVersionEsperada()
    {
        var encabezado = Request.Headers.IfMatch.ToString().Trim();
        return encabezado.Length == 0 ? null : encabezado.Trim('"');
    }
    [HttpPost("sugerencia")]
    [Authorize(Policy = Politicas.OtCrear)]
    [EnableRateLimiting(PoliticasDeLimite.Sugerencias)]
    public async Task<IActionResult> SolicitarSugerencia(SolicitudSugerenciaDto solicitud,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(solicitud.Descripcion))
        {
            return this.RespuestaDeError(400, "DESCRIPCION_OBLIGATORIA", "La descripción de la tarea es obligatoria.");
        }

        var cronometro = System.Diagnostics.Stopwatch.StartNew();
        var resultadoDeLaSugerencia = "error";
        try
        {
            var sugerencia = await _servicioSugerencias.BuscarAsync(
                solicitud.Descripcion, solicitud.Numero, solicitud.ActivoId, cancellationToken);
            if (sugerencia is null)
            {
                resultadoDeLaSugerencia = "sin_contexto";
                return this.RespuestaDeError(422, "SIN_CONTEXTO_TECNICO", "No hay información suficiente en la documentación técnica para esa tarea.");
            }

            await _ordenTrabajoDao.RegistrarSugerenciaGeneradaAsync(solicitud.Sesion, solicitud.Descripcion.Trim(),
                solicitud.Numero, sugerencia.Texto, sugerencia.Modo, cancellationToken);
            resultadoDeLaSugerencia = sugerencia.Modo == ModosDeSugerencia.Ia ? "ok" : sugerencia.Modo;
            return Ok(new { sugerencia = sugerencia.Texto, modo = sugerencia.Modo });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return StatusCode(StatusCodeClienteCerroLaSolicitud);
        }
        catch (ModeloLocalNoDisponibleException excepcion)
        {
            // Sin los modelos locales no se puede buscar en la documentación: es un problema de instalación.
            resultadoDeLaSugerencia = "no_disponible";
            _logger.LogError(excepcion, "Faltan los modelos de IA locales al pedir una sugerencia");
            return this.RespuestaDeError(502, "SERVICIO_IA_NO_DISPONIBLE", "El servicio de IA no está disponible en este momento. Intentá de nuevo más tarde.");
        }
        catch (Exception excepcion) when (TraduccionDeErroresDeNegocio.Traducir(excepcion) is null)
        {
            _logger.LogError(excepcion, "Falló la generación de una sugerencia técnica");
            return this.RespuestaDeError(502, "SUGERENCIA_FALLIDA", "No se pudo obtener la sugerencia técnica.");
        }
        finally
        {
            MetricasDeNegocio.DuracionDeSugerencias.Record(cronometro.Elapsed.TotalSeconds,
                new KeyValuePair<string, object?>("resultado", resultadoDeLaSugerencia));
        }
    }

    private static void ValidarFecha(DateOnly fecha)
    {
        if (fecha == default)
        {
            throw new ArgumentException("La fecha prevista de ejecución es obligatoria.");
        }
    }
}
