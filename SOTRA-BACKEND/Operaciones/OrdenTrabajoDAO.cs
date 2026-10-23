using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones;

public sealed record CambiosDeOrden(
    long ClienteId, long ActivoId, long? TecnicoId, bool Tipo, short Prioridad, string Descripcion, string? Resultado);

public sealed record OrdenResumen(
    long Id, DateTime CreadoEn, DateTime? FechaInicio, DateTime? FechaCierre, long ClienteId, string Cliente,
    long ActivoId, string Activo, long? TecnicoId, string? Tecnico, bool EsPreventivo, short Prioridad, string Estado);

public sealed record RepuestoUsadoEnOrdenes(string Codigo, string Nombre, int Cantidad);

public class OrdenTrabajoDAO
{
    private const string FormatoDeFecha = "yyyy-MM-dd";
    public static readonly ColumnasOrdenables<OrdenTrabajo> ColumnasOrdenables = new(
        o => o.IdOrden,
        [
            new("id", o => o.IdOrden),
            new("nombreCliente", o => o.IdClienteNavigation.RazonSocial),
            new("nombreActivo", o => o.IdActivoNavigation.Nombre ?? o.IdActivoNavigation.NumSerie),
            new("tipo", o => o.Tipo),
            new("prioridad", o => o.Prioridad),
            new("nombreTecnico", o => o.IdUsuarioTecnicoNavigation == null
                ? null
                : o.IdUsuarioTecnicoNavigation.Nombre + " " + o.IdUsuarioTecnicoNavigation.Apellido),
            new("estado", o => o.HistorialEstadoOts
                .OrderByDescending(h => h.FechaHora).ThenByDescending(h => h.IdHEstadoOt)
                .Select(h => h.IdEstadoOtNavigation.Nombre).FirstOrDefault() ?? "creada"),
            new("creadoEn", o => o.FechaCreada),
            // La fecha prevista se guarda como texto ISO (aaaa-mm-dd) en la observaciÃ³n del primer estado.
            new("fechaPrevistaEjecucion", o => o.HistorialEstadoOts
                .OrderBy(h => h.FechaHora).ThenBy(h => h.IdHEstadoOt)
                .Select(h => h.Observacion).FirstOrDefault()),
        ]);


    private static readonly string[] EstadosValidos =
    {
        "creada", "asignada", "en_ejecucion", "pausada", "finalizada", "cerrada", "cancelada"
    };

    private static readonly Dictionary<string, string[]> TransicionesPermitidas = new()
    {
        { "creada", new[] { "asignada", "cancelada" } },
        { "asignada", new[] { "en_ejecucion", "cancelada" } },
        { "en_ejecucion", new[] { "pausada", "finalizada" } },
        { "pausada", new[] { "en_ejecucion", "cancelada" } },
        { "finalizada", new[] { "cerrada", "asignada" } },
        { "cerrada", new string[0] },
        { "cancelada", new string[0] }
    };

    private readonly SotraContext _contexto;

    public OrdenTrabajoDAO(SotraContext contexto)
    {
        _contexto = contexto;
    }

    public async Task<List<OrdenTrabajo>> ListarTodoAsync(CancellationToken cancelacion)
    {
        return await ConsultaCompleta().AsNoTracking().OrderBy(orden => orden.IdOrden).ToListAsync(cancelacion);
    }

    /// <summary>Órdenes que cumplen el filtro, sin ordenar ni paginar. Es la base del listado y de los reportes.</summary>
    public IQueryable<OrdenTrabajo> Filtrar(FiltroOrdenesTrabajo filtro)
    {
        var consulta = _contexto.OrdenTrabajos.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Buscar))
        {
            var texto = filtro.Buscar.Trim();
            var patron = ConsultasDeTexto.PatronContiene(texto);
            var esNumero = long.TryParse(texto, out var idBuscado);
            consulta = consulta.Where(o => (esNumero && o.IdOrden == idBuscado)
                || EF.Functions.ILike(o.Descripcion, patron)
                || EF.Functions.ILike(o.IdClienteNavigation.RazonSocial, patron)
                || EF.Functions.ILike(o.IdActivoNavigation.Nombre ?? "", patron)
                || EF.Functions.ILike(o.IdActivoNavigation.NumSerie, patron)
                || (o.IdUsuarioTecnicoNavigation != null
                    && EF.Functions.ILike(o.IdUsuarioTecnicoNavigation.Nombre + " " + o.IdUsuarioTecnicoNavigation.Apellido, patron)));
        }

        if (filtro.ClienteId is { } clienteId) consulta = consulta.Where(o => o.IdCliente == clienteId);
        if (filtro.ActivoId is { } activoId) consulta = consulta.Where(o => o.IdActivo == activoId);
        if (filtro.TecnicoId is { } tecnicoId) consulta = consulta.Where(o => o.IdUsuarioTecnico == tecnicoId);

        if (!string.IsNullOrEmpty(filtro.Estado))
        {
            var estado = filtro.Estado;
            consulta = consulta.Where(o => (o.HistorialEstadoOts
                .OrderByDescending(h => h.FechaHora).ThenByDescending(h => h.IdHEstadoOt)
                .Select(h => h.IdEstadoOtNavigation.Nombre).FirstOrDefault() ?? "creada") == estado);
        }

        consulta = AplicarPrioridad(consulta, filtro.Prioridad);

        if (filtro.EsPreventivo is { } esPreventivo) consulta = consulta.Where(o => o.Tipo == esPreventivo);

        if (filtro.ConSugerencias == true)
        {
            consulta = consulta.Where(o => o.HistorialRecomendacionesIa.Any(r => r.Estado));
        }

        if (filtro.FechaDesde is { } desde)
        {
            var inicio = desde.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            consulta = consulta.Where(o => o.FechaCreada >= inicio);
        }

        if (filtro.FechaHasta is { } hasta)
        {
            var finExclusivo = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            consulta = consulta.Where(o => o.FechaCreada < finExclusivo);
        }

        return consulta;
    }

    public async Task<(List<OrdenTrabajo> Elementos, int Total)> ListarPaginaAsync(
        FiltroOrdenesTrabajo filtro, int pagina, int tamanoPagina, CancellationToken cancelacion)
    {
        var consulta = Filtrar(filtro);

        IOrderedQueryable<OrdenTrabajo> ordenada;
        if (!string.IsNullOrEmpty(filtro.OrdenarPor))
        {
            ordenada = ColumnasOrdenables.Aplicar(consulta, filtro.OrdenarPor, filtro.Descendente);
        }
        else if (filtro.OrdenarPorCreacion)
        {
            ordenada = filtro.Descendente
                ? consulta.OrderByDescending(o => o.FechaCreada).ThenByDescending(o => o.IdOrden)
                : consulta.OrderBy(o => o.FechaCreada).ThenBy(o => o.IdOrden);
        }
        else
        {
            ordenada = filtro.Descendente ? consulta.OrderByDescending(o => o.IdOrden) : consulta.OrderBy(o => o.IdOrden);
        }

        var total = await ordenada.CountAsync(cancelacion);
        var idsDeLaPagina = await ordenada.Skip((pagina - 1) * tamanoPagina).Take(tamanoPagina)
            .Select(o => o.IdOrden).ToListAsync(cancelacion);

        var porId = await ConsultaCompleta().AsNoTracking().AsSplitQuery()
            .Where(o => idsDeLaPagina.Contains(o.IdOrden))
            .ToDictionaryAsync(o => o.IdOrden, cancelacion);
        var elementos = idsDeLaPagina.Select(id => porId[id]).ToList();
        return (elementos, total);
    }

    /// <summary>Datos mínimos de cada orden filtrada, resueltos en la base con una sola consulta (sin cargar el grafo completo).</summary>
    public async Task<List<OrdenResumen>> ListarResumenesAsync(FiltroOrdenesTrabajo filtro, CancellationToken cancelacion)
    {
        return await Filtrar(filtro)
            .OrderBy(o => o.IdOrden)
            .Select(o => new OrdenResumen(
                o.IdOrden, o.FechaCreada, o.FechaInicio, o.FechaCierre,
                o.IdCliente, o.IdClienteNavigation.RazonSocial,
                o.IdActivo, string.IsNullOrWhiteSpace(o.IdActivoNavigation.Nombre) ? o.IdActivoNavigation.NumSerie : o.IdActivoNavigation.Nombre,
                o.IdUsuarioTecnico,
                o.IdUsuarioTecnicoNavigation == null ? null : o.IdUsuarioTecnicoNavigation.Nombre + " " + o.IdUsuarioTecnicoNavigation.Apellido,
                o.Tipo, o.Prioridad,
                o.HistorialEstadoOts.OrderByDescending(h => h.FechaHora).ThenByDescending(h => h.IdHEstadoOt)
                    .Select(h => h.IdEstadoOtNavigation.Nombre).FirstOrDefault() ?? "creada"))
            .ToListAsync(cancelacion);
    }

    /// <summary>Cuántas órdenes filtradas usaron cada repuesto, agrupado en la base.</summary>
    public async Task<List<RepuestoUsadoEnOrdenes>> ListarRepuestosUsadosAsync(FiltroOrdenesTrabajo filtro, CancellationToken cancelacion)
    {
        return await Filtrar(filtro)
            .SelectMany(o => o.OtRepuestos)
            .GroupBy(r => new { r.IdRepuestoNavigation.Codigo, r.IdRepuestoNavigation.Nombre })
            .Select(g => new RepuestoUsadoEnOrdenes(g.Key.Codigo, g.Key.Nombre, g.Count()))
            .OrderByDescending(r => r.Cantidad).ThenBy(r => r.Codigo)
            .ToListAsync(cancelacion);
    }

    public Task<string?> ObtenerNombreDeClienteAsync(long id, CancellationToken cancelacion) =>
        _contexto.Clientes.AsNoTracking().Where(c => c.IdCliente == id).Select(c => c.RazonSocial)
            .FirstOrDefaultAsync(cancelacion);

    public Task<string?> ObtenerNombreDeActivoAsync(long id, CancellationToken cancelacion) =>
        _contexto.Activos.AsNoTracking().Where(a => a.IdActivo == id)
            .Select(a => string.IsNullOrWhiteSpace(a.Nombre) ? a.NumSerie : a.Nombre).FirstOrDefaultAsync(cancelacion);

    public Task<string?> ObtenerNombreDeTecnicoAsync(long id, CancellationToken cancelacion) =>
        _contexto.Usuarios.AsNoTracking().Where(u => u.IdUsuario == id).Select(u => u.Nombre + " " + u.Apellido)
            .FirstOrDefaultAsync(cancelacion);

    private static IQueryable<OrdenTrabajo> AplicarPrioridad(IQueryable<OrdenTrabajo> consulta, string? prioridad)
    {
        return prioridad switch
        {
            "baja" => consulta.Where(o => o.Prioridad < 3),
            "media" => consulta.Where(o => o.Prioridad >= 3 && o.Prioridad < 5),
            "alta" => consulta.Where(o => o.Prioridad >= 5),
            null or "" => consulta,
            _ => consulta.Where(o => false),
        };
    }

    public async Task<OrdenTrabajo?> SeleccionarAsync(long id, CancellationToken cancelacion)
    {
        return await ConsultaCompleta().FirstOrDefaultAsync(orden => orden.IdOrden == id, cancelacion);
    }

    // idResponsable es el usuario que crea la orden. Si es null (pedido sin token, con
    // AUTH_OBLIGATORIA=false), se asigna el primer jefe de mantenimiento activo.
    public async Task<OrdenTrabajo> CrearAsync(OrdenTrabajo orden, DateOnly fechaPrevista, List<long> repuestosIds,
        List<string> sugerencias, Guid? sesion, long? idResponsable, CancellationToken cancelacion)
    {
        await ValidarRelacionesAsync(orden.IdCliente, orden.IdActivo, orden.IdUsuarioTecnico, repuestosIds, cancelacion);
        orden.IdUsuarioResponsable = idResponsable ?? await ObtenerResponsableAsync(cancelacion);
        orden.FechaCreada = DateTime.UtcNow;

        await using var transaccion = await _contexto.Database.BeginTransactionAsync(cancelacion);
        _contexto.OrdenTrabajos.Add(orden);
        await _contexto.SaveChangesAsync(cancelacion);

        await AgregarEstadoAsync(orden, "creada", fechaPrevista.ToString(FormatoDeFecha), cancelacion);
        ReemplazarRepuestos(orden, repuestosIds);
        await ReemplazarSugerenciasAsync(orden, sugerencias, sesion, cancelacion);
        await _contexto.SaveChangesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);

        return (await SeleccionarAsync(orden.IdOrden, cancelacion))!;
    }

    /// <summary>
    /// Modifica una orden bloqueando su fila: dos modificaciones simultáneas se ejecutan en serie y la segunda
    /// valida contra el estado que dejó la primera. Si se informa <paramref name="coincideConVersionVigente"/> y la
    /// orden cambió desde que la persona la leyó, falla con <c>ORDEN_MODIFICADA</c>. Devuelve <c>null</c> si la orden no existe.
    /// </summary>
    public async Task<OrdenTrabajo?> ActualizarAsync(long idOrden, CambiosDeOrden cambios, DateOnly fechaPrevista,
        List<long> repuestosIds, List<string> sugerencias, string estado, string? motivo, Guid? sesion,
        Func<OrdenTrabajo, bool>? coincideConVersionVigente, CancellationToken cancelacion)
    {
        await using var transaccion = await _contexto.Database.BeginTransactionAsync(cancelacion);
        if (!await _contexto.OrdenTrabajos.AsNoTracking().AnyAsync(o => o.IdOrden == idOrden, cancelacion))
        {
            return null;
        }

        var orden = await BloquearYRecargarAsync(idOrden, cancelacion);
        if (coincideConVersionVigente is not null && !coincideConVersionVigente(orden))
        {
            throw new ErrorDeOrdenException(ErrorDeOrdenException.OrdenModificada);
        }

        var estadoActual = ObtenerEstadoActual(orden);
        if (estadoActual == "cerrada")
        {
            throw new ErrorDeOrdenException(ErrorDeOrdenException.OrdenCerrada);
        }

        orden.IdCliente = cambios.ClienteId;
        orden.IdActivo = cambios.ActivoId;
        orden.IdUsuarioTecnico = cambios.TecnicoId;
        orden.Tipo = cambios.Tipo;
        orden.Prioridad = cambios.Prioridad;
        orden.Descripcion = cambios.Descripcion;
        orden.Resultado = cambios.Resultado;

        ValidarEstado(estado);
        ValidarTransicion(estadoActual, estado, orden.IdUsuarioTecnico, motivo);
        await ValidarRelacionesAsync(orden.IdCliente, orden.IdActivo, orden.IdUsuarioTecnico, repuestosIds, cancelacion);

        var primerEstado = orden.HistorialEstadoOts.OrderBy(item => item.FechaHora).FirstOrDefault();
        if (primerEstado is not null)
        {
            primerEstado.Observacion = fechaPrevista.ToString(FormatoDeFecha);
        }

        if (estado != estadoActual)
        {
            await AgregarEstadoAsync(orden, estado, motivo, cancelacion);
            AplicarFechasDeEstado(orden, estado);
        }

        ReemplazarRepuestos(orden, repuestosIds);
        await ReemplazarSugerenciasAsync(orden, sugerencias, sesion, cancelacion);
        await _contexto.SaveChangesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);

        return (await SeleccionarAsync(orden.IdOrden, cancelacion))!;
    }

    public async Task<OrdenTrabajo> CambiarEstadoAsync(
        OrdenTrabajo ordenLeida, string estado, string? motivo, CancellationToken cancelacion)
    {
        await using var transaccion = await _contexto.Database.BeginTransactionAsync(cancelacion);
        var orden = await BloquearYRecargarAsync(ordenLeida.IdOrden, cancelacion);

        var estadoActual = ObtenerEstadoActual(orden);
        if (estadoActual == "cerrada")
        {
            throw new ErrorDeOrdenException(ErrorDeOrdenException.OrdenCerrada);
        }

        ValidarEstado(estado);
        if (estado == estadoActual)
        {
            throw new ErrorDeOrdenException(ErrorDeOrdenException.TransicionInvalida);
        }

        ValidarTransicion(estadoActual, estado, orden.IdUsuarioTecnico, motivo);
        await AgregarEstadoAsync(orden, estado, motivo, cancelacion);
        AplicarFechasDeEstado(orden, estado);
        await _contexto.SaveChangesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);

        return (await SeleccionarAsync(orden.IdOrden, cancelacion))!;
    }

    public async Task<OrdenTrabajo> AsignarTecnicoAsync(OrdenTrabajo ordenLeida, long tecnicoId, CancellationToken cancelacion)
    {
        await using var transaccion = await _contexto.Database.BeginTransactionAsync(cancelacion);
        var orden = await BloquearYRecargarAsync(ordenLeida.IdOrden, cancelacion);

        var estadoActual = ObtenerEstadoActual(orden);
        if (estadoActual == "cerrada" || estadoActual == "cancelada")
        {
            throw new ErrorDeOrdenException(ErrorDeOrdenException.OrdenNoModificable);
        }

        var tecnico = await ValidarTecnicoAsync(tecnicoId, cancelacion);
        orden.IdUsuarioTecnico = tecnicoId;

        var estadoNuevo = estadoActual;
        if (estadoActual == "creada")
        {
            estadoNuevo = "asignada";
        }

        await AgregarEstadoAsync(orden, estadoNuevo, "Técnico asignado: " + tecnico.Nombre + " " + tecnico.Apellido, cancelacion);
        await _contexto.SaveChangesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);

        return (await SeleccionarAsync(orden.IdOrden, cancelacion))!;
    }

    /// <summary>
    /// Bloquea la fila de la orden hasta el fin de la transacción y la vuelve a leer: dos cambios simultáneos
    /// se ejecutan uno detrás de otro y el segundo valida contra el estado ya modificado por el primero.
    /// </summary>
    private async Task<OrdenTrabajo> BloquearYRecargarAsync(long idOrden, CancellationToken cancelacion)
    {
        await _contexto.OrdenTrabajos
            .FromSqlInterpolated($"SELECT * FROM operativo.orden_trabajo WHERE id_orden = {idOrden} FOR UPDATE")
            .AsNoTracking()
            .ToListAsync(cancelacion);
        _contexto.ChangeTracker.Clear();
        return await SeleccionarAsync(idOrden, cancelacion) ?? throw new ErrorDeOrdenException(ErrorDeOrdenException.OrdenInexistente);
    }

    public static List<string> ObtenerEstadosSiguientes(string estadoActual)
    {
        var estadosSiguientes = new List<string>();
        if (TransicionesPermitidas.ContainsKey(estadoActual))
        {
            estadosSiguientes.AddRange(TransicionesPermitidas[estadoActual]);
        }

        return estadosSiguientes;
    }

    private static void ValidarTransicion(string estadoActual, string estadoNuevo, long? tecnicoId, string? motivo)
    {
        if (estadoNuevo == estadoActual)
        {
            return;
        }

        if (!ObtenerEstadosSiguientes(estadoActual).Contains(estadoNuevo))
        {
            throw new ErrorDeOrdenException(ErrorDeOrdenException.TransicionInvalida);
        }

        if ((estadoNuevo == "asignada" || estadoNuevo == "en_ejecucion") && tecnicoId is null)
        {
            throw new ErrorDeOrdenException(ErrorDeOrdenException.TecnicoObligatorio);
        }

        var necesitaMotivo = estadoNuevo == "pausada" || estadoNuevo == "cancelada" || estadoNuevo == "cerrada"
                             || (estadoActual == "finalizada" && estadoNuevo == "asignada");
        if (necesitaMotivo && string.IsNullOrWhiteSpace(motivo))
        {
            throw new ErrorDeOrdenException(ErrorDeOrdenException.MotivoObligatorio);
        }
    }

    private static void AplicarFechasDeEstado(OrdenTrabajo orden, string estado)
    {
        if (estado == "en_ejecucion" && orden.FechaInicio is null)
        {
            orden.FechaInicio = DateTime.UtcNow;
        }

        if (estado == "cerrada")
        {
            orden.FechaCierre = DateTime.UtcNow;
        }
    }

    private IQueryable<OrdenTrabajo> ConsultaCompleta()
    {
        return _contexto.OrdenTrabajos
            .Include(orden => orden.IdClienteNavigation)
            .Include(orden => orden.IdActivoNavigation)
            .Include(orden => orden.IdUsuarioTecnicoNavigation)
            .Include(orden => orden.HistorialEstadoOts)
                .ThenInclude(historial => historial.IdEstadoOtNavigation)
            .Include(orden => orden.HistorialRecomendacionesIa)
            .Include(orden => orden.OtRepuestos)
                .ThenInclude(relacion => relacion.IdRepuestoNavigation);
    }

    private async Task ValidarRelacionesAsync(
        long clienteId, long activoId, long? tecnicoId, List<long> repuestosIds, CancellationToken cancelacion)
    {
        var cliente = await _contexto.Clientes.AsNoTracking()
            .FirstOrDefaultAsync(item => item.IdCliente == clienteId, cancelacion);
        if (cliente is null || !cliente.Estado)
        {
            throw new ValidacionDeNegocioException("El cliente indicado no existe o está inactivo.");
        }

        var activo = await _contexto.Activos.AsNoTracking()
            .FirstOrDefaultAsync(item => item.IdActivo == activoId, cancelacion);
        if (activo is null || !activo.Estado || activo.IdCliente != clienteId)
        {
            throw new ValidacionDeNegocioException("El activo indicado no pertenece al cliente o está inactivo.");
        }

        if (tecnicoId.HasValue)
        {
            await ValidarTecnicoAsync(tecnicoId.Value, cancelacion);
        }

        foreach (var repuestoId in repuestosIds.Distinct())
        {
            var repuesto = await _contexto.Repuestos.AsNoTracking()
                .FirstOrDefaultAsync(item => item.IdRepuesto == repuestoId, cancelacion);
            if (repuesto is null || !repuesto.Estado)
            {
                throw new ValidacionDeNegocioException("Uno de los repuestos no existe o está inactivo.");
            }
        }
    }

    private async Task<Usuario> ValidarTecnicoAsync(long tecnicoId, CancellationToken cancelacion)
    {
        var tecnico = await _contexto.Usuarios.AsNoTracking()
            .Include(item => item.IdRols)
            .FirstOrDefaultAsync(item => item.IdUsuario == tecnicoId, cancelacion);
        if (tecnico is null || !tecnico.Estado)
        {
            throw new ValidacionDeNegocioException("El técnico indicado no existe o está inactivo.");
        }

        var tieneRolTecnico = tecnico.IdRols.Any(rol => rol.Nombre == "Técnico" && rol.Activo);
        if (!tieneRolTecnico)
        {
            throw new ValidacionDeNegocioException("El usuario indicado no tiene el rol Técnico activo.");
        }

        return tecnico;
    }

    private async Task<long> ObtenerResponsableAsync(CancellationToken cancelacion)
    {
        var jefe = await _contexto.Usuarios.AsNoTracking()
            .Where(item => item.Estado)
            .Where(item => item.IdRols.Any(rol => rol.Nombre == "Jefe de mantenimiento" && rol.Activo))
            .OrderBy(item => item.IdUsuario)
            .FirstOrDefaultAsync(cancelacion);
        if (jefe is null)
        {
            throw new ValidacionDeNegocioException("No existe un jefe de mantenimiento activo para asignar como responsable.");
        }

        return jefe.IdUsuario;
    }

    private async Task AgregarEstadoAsync(
        OrdenTrabajo orden, string nombreEstado, string? observacion, CancellationToken cancelacion)
    {
        ValidarEstado(nombreEstado);
        var estado = await _contexto.EstadoOts.FirstOrDefaultAsync(item => item.Nombre == nombreEstado, cancelacion);
        if (estado is null)
        {
            estado = new EstadoOt { Nombre = nombreEstado, Estado = true };
            _contexto.EstadoOts.Add(estado);
        }

        orden.HistorialEstadoOts.Add(new HistorialEstadoOt
        {
            IdEstadoOtNavigation = estado,
            FechaHora = DateTime.UtcNow,
            Observacion = observacion
        });
    }

    private static void ValidarEstado(string estado)
    {
        if (!EstadosValidos.Contains(estado))
        {
            throw new ValidacionDeNegocioException("El estado indicado no es válido.");
        }
    }

    private static string ObtenerEstadoActual(OrdenTrabajo orden)
    {
        var ultimoEstado = orden.HistorialEstadoOts
            .OrderByDescending(item => item.FechaHora)
            .ThenByDescending(item => item.IdHEstadoOt)
            .FirstOrDefault();
        if (ultimoEstado is null)
        {
            return "creada";
        }

        return ultimoEstado.IdEstadoOtNavigation.Nombre;
    }

    private void ReemplazarRepuestos(OrdenTrabajo orden, List<long> repuestosIds)
    {
        _contexto.OtRepuestos.RemoveRange(orden.OtRepuestos);
        orden.OtRepuestos.Clear();
        foreach (var repuestoId in repuestosIds.Distinct())
        {
            orden.OtRepuestos.Add(new OtRepuesto { IdRepuesto = repuestoId });
        }
    }

    public async Task RegistrarSugerenciaGeneradaAsync(
        Guid? sesion, string consulta, int numeroIntento, string texto, CancellationToken cancelacion)
    {
        _contexto.HistorialRecomendacionesIa.Add(new HistorialRecomendacionIa
        {
            Recomendacion = texto.Trim(),
            Estado = false,
            Sesion = sesion,
            Consulta = consulta,
            NumeroIntento = numeroIntento,
            FechaGeneracion = DateTime.UtcNow,
            Resultado = "pendiente"
        });
        await _contexto.SaveChangesAsync(cancelacion);
    }

    private async Task ReemplazarSugerenciasAsync(
        OrdenTrabajo orden, List<string> sugerencias, Guid? sesion, CancellationToken cancelacion)
    {
        var textosNuevos = new List<string>();
        foreach (var texto in sugerencias)
        {
            if (!string.IsNullOrWhiteSpace(texto))
            {
                textosNuevos.Add(texto.Trim());
            }
        }

        foreach (var sugerenciaAnterior in orden.HistorialRecomendacionesIa)
        {
            if (!sugerenciaAnterior.Estado)
            {
                continue;
            }

            if (textosNuevos.Contains(sugerenciaAnterior.Recomendacion))
            {
                textosNuevos.Remove(sugerenciaAnterior.Recomendacion);
            }
            else
            {
                sugerenciaAnterior.Estado = false;
            }
        }

        if (sesion is not null)
        {
            var sugerenciasDeLaSesion = await _contexto.HistorialRecomendacionesIa
                .Where(sugerencia => sugerencia.Sesion == sesion && sugerencia.IdOrden == null)
                .OrderBy(sugerencia => sugerencia.IdRecomendacionIa)
                .ToListAsync(cancelacion);

            foreach (var sugerenciaGenerada in sugerenciasDeLaSesion)
            {
                sugerenciaGenerada.IdOrden = orden.IdOrden;
                if (textosNuevos.Contains(sugerenciaGenerada.Recomendacion))
                {
                    textosNuevos.Remove(sugerenciaGenerada.Recomendacion);
                    sugerenciaGenerada.Estado = true;
                    sugerenciaGenerada.Resultado = "aceptada";
                }
                else
                {
                    sugerenciaGenerada.Estado = false;
                    sugerenciaGenerada.Resultado = "descartada";
                }
            }
        }

        foreach (var texto in textosNuevos)
        {
            orden.HistorialRecomendacionesIa.Add(new HistorialRecomendacionIa
            {
                Recomendacion = texto,
                Estado = true,
                FechaGeneracion = DateTime.UtcNow,
                Resultado = "aceptada"
            });
        }
    }
}
