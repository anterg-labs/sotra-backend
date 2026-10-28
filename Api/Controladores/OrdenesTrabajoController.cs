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

    /// <summary>Lista las órdenes de trabajo con filtros por texto, cliente, activo, técnico, estado, fase, prioridad, tipo y fechas.</summary>
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
            OrdenarPorCreacion: false, Descendente: filtros.DireccionOrden == "desc", OrdenarPor: filtros.OrdenarPor, Fase: filtros.Fase,
            SoloActivas: filtros.SoloActivas, SinAsignar: filtros.SinAsignar);

        if (filtros.Tipo is not (null or "" or "preventivo" or "correctivo"))
        {
            return Ok(PaginaDto<OrdenTrabajoDto>.Crear([], 0, filtros.Pagina, filtros.TamanoPagina));
        }

        var (ordenes, total) = await _ordenTrabajoDao.ListarPaginaAsync(filtro, filtros.Pagina, filtros.TamanoPagina, cancelacion);
        var reglas = await _ordenTrabajoDao.ObtenerReglasAsync(cancelacion);
        var elementos = ordenes.Select(orden => OrdenTrabajoMapeador.ADto(orden, reglas, User.EsSoloTecnico())).ToList();
        return Ok(PaginaDto<OrdenTrabajoDto>.Crear(elementos, total, filtros.Pagina, filtros.TamanoPagina));
    }

    /// <summary>
    /// Órdenes de un rango de fechas (hasta 100 días) ubicadas por su fecha prevista de ejecución, para la vista de
    /// calendario. Las eliminadas no se muestran salvo que se habiliten.
    /// </summary>
    [HttpGet("calendario")]
    [Authorize(Policy = Politicas.OtConsultar)]
    [ProducesResponseType<CalendarioOrdenesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CalendarioOrdenesDto>> ObtenerCalendario([FromQuery] FiltrosCalendarioDto filtros, CancellationToken cancelacion)
    {
        const int maximoDeDias = 100;
        const int maximoDeOrdenes = 3000;
        var desde = filtros.Desde!.Value;
        var hasta = filtros.Hasta!.Value;
        if (desde > hasta || hasta.DayNumber - desde.DayNumber + 1 > maximoDeDias)
        {
            return this.RespuestaDeError(400, "RANGO_FECHAS_INVALIDO",
                "La fecha desde no puede ser posterior a la fecha hasta y el rango no puede superar los " + maximoDeDias + " días.");
        }

        if (filtros.Tipo is not (null or "" or "preventivo" or "correctivo"))
        {
            return Ok(new CalendarioOrdenesDto { Desde = desde.ToString("yyyy-MM-dd"), Hasta = hasta.ToString("yyyy-MM-dd"), Elementos = [] });
        }

        var tecnicoId = (long?)null;
        if (User.EsSoloTecnico())
        {
            tecnicoId = await _usuarioActual.ObtenerIdUsuarioAsync(cancelacion);
            if (tecnicoId is null)
            {
                return Ok(new CalendarioOrdenesDto { Desde = desde.ToString("yyyy-MM-dd"), Hasta = hasta.ToString("yyyy-MM-dd"), Elementos = [] });
            }
        }

        var filtro = new FiltroOrdenesTrabajo(
            null, filtros.ClienteId, filtros.ActivoId, tecnicoId, filtros.Estado, filtros.Prioridad,
            filtros.Tipo is null or "" ? null : filtros.Tipo == "preventivo", null, null, null,
            OrdenarPorCreacion: false, Descendente: false, SinAsignar: filtros.SinAsignar);
        var filas = await _ordenTrabajoDao.ListarCalendarioAsync(filtro, desde, hasta, filtros.IncluirEliminadas, maximoDeOrdenes, cancelacion);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var elementos = filas.Take(maximoDeOrdenes).Select(fila => new CalendarioOrdenDto
        {
            Id = fila.Id,
            Fecha = fila.Fecha.ToString("yyyy-MM-dd"),
            NombreActivo = fila.NombreActivo,
            NombreCliente = fila.NombreCliente,
            Tipo = fila.EsPreventivo ? "preventivo" : "correctivo",
            Prioridad = OrdenTrabajoMapeador.APrioridadTexto(fila.Prioridad),
            Estado = fila.Estado,
            Fase = fila.Fase,
            Vencida = fila.Fecha < hoy && fila.Fase is "pendiente" or "en_curso" or "por_validar",
        }).ToList();
        return Ok(new CalendarioOrdenesDto
        {
            Desde = desde.ToString("yyyy-MM-dd"),
            Hasta = hasta.ToString("yyyy-MM-dd"),
            Truncado = filas.Count > maximoDeOrdenes,
            Elementos = elementos,
        });
    }

    /// <summary>Catálogo de estados de las órdenes con su fase y reglas.</summary>
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

    /// <summary>Catálogo de motivos codificados para pausas y devoluciones.</summary>
    [HttpGet("motivos")]
    [Authorize(Policy = Politicas.OtConsultar)]
    [ProducesResponseType<List<MotivoOrdenTrabajoDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<MotivoOrdenTrabajoDto>>> ObtenerMotivos(CancellationToken cancelacion)
    {
        var motivos = await _ordenTrabajoDao.ListarMotivosAsync(cancelacion);
        return Ok(motivos.Select(motivo => new MotivoOrdenTrabajoDto(motivo.Codigo!, motivo.Descripcion ?? motivo.Codigo!)).ToList());
    }

    /// <summary>Devuelve el detalle de una orden de trabajo, con su historial.</summary>
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

    /// <summary>Crea una orden de trabajo.</summary>
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
            Resultado = TextoOpcional.Normalizar(solicitud.Observaciones),
            TiempoEstimadoHoras = solicitud.TiempoEstimadoHoras,
            IdCausaFalla = solicitud.CausaFallaId,
            LecturaUso = solicitud.LecturaUso
        };

        var ordenCreada = await _ordenTrabajoDao.CrearAsync(orden, solicitud.FechaPrevistaEjecucion,
            APedidos(solicitud.Repuestos), solicitud.Sugerencias, solicitud.Sesion,
            await _usuarioActual.ObtenerIdUsuarioAsync(cancelacion), cancelacion);
        var respuesta = OrdenTrabajoMapeador.ADto(ordenCreada, await _ordenTrabajoDao.ObtenerReglasAsync(cancelacion));
        return CreatedAtAction(nameof(ObtenerPorId), new { id = ordenCreada.IdOrden }, respuesta);
    }

    /// <summary>Modifica una orden de trabajo (con control de versión).</summary>
    [HttpPut("{id:long}")]
    [Authorize(Policy = Politicas.OtEditar)]
    public async Task<ActionResult<OrdenTrabajoDto>> Actualizar(long id, OrdenTrabajoActualizarDto solicitud, CancellationToken cancelacion)
    {
        ValidarFecha(solicitud.FechaPrevistaEjecucion);
        var cambios = new CambiosDeOrden(
            solicitud.ClienteId, solicitud.ActivoId, solicitud.TecnicoId,
            OrdenTrabajoMapeador.ATipoBooleano(solicitud.Tipo),
            OrdenTrabajoMapeador.APrioridadNumero(solicitud.Prioridad),
            solicitud.Descripcion.Trim(), TextoOpcional.Normalizar(solicitud.Observaciones),
            solicitud.TiempoEstimadoHoras, solicitud.CausaFallaId, solicitud.LecturaUso, solicitud.MotivoCodigo);

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

    /// <summary>Reemplaza los repuestos y cantidades de una orden.</summary>
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

    /// <summary>Cambia el estado de una orden y registra quién, cuándo y el motivo.</summary>
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
            await _usuarioActual.ObtenerIdUsuarioAsync(cancelacion), cancelacion, solicitud.LecturaUso, solicitud.MotivoCodigo);
        return Ok(OrdenTrabajoMapeador.ADto(ordenActualizada, await _ordenTrabajoDao.ObtenerReglasAsync(cancelacion), User.EsSoloTecnico()));
    }

    /// <summary>Asigna o reasigna el técnico de una orden.</summary>
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
    /// <summary>Pide a la IA sugerencias para una orden a partir de la documentación técnica.</summary>
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
