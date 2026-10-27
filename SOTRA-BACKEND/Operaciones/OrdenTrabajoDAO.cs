using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones;

/// <remarks>Los datos para los reportes son opcionales: <c>null</c> deja el valor guardado como está.</remarks>
public sealed record CambiosDeOrden(
    long ClienteId, long ActivoId, long? TecnicoId, bool Tipo, short Prioridad, string Descripcion, string? Resultado,
    decimal? TiempoEstimadoHoras = null, long? CausaFallaId = null, decimal? LecturaUso = null, string? MotivoCodigo = null);

public sealed record OrdenResumen(
    long Id, DateTime CreadoEn, DateTime? FechaInicio, DateTime? FechaCierre, long ClienteId, string Cliente,
    long ActivoId, string Activo, long? TecnicoId, string? Tecnico, bool EsPreventivo, short Prioridad, string Estado);

public sealed record RepuestoUsadoEnOrdenes(string Codigo, string Nombre, int Cantidad);

public sealed record OrdenDeCalendario(
    long Id, DateOnly Fecha, string NombreActivo, string NombreCliente, bool EsPreventivo, short Prioridad, string Estado, string Fase);

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
                .Select(h => h.IdEstadoOtNavigation.Nombre).FirstOrDefault() ?? ""),
            new("creadoEn", o => o.FechaCreada),
            new("fechaPrevistaEjecucion", o => o.FechaPrevistaEjecucion),
        ]);

    private static readonly string[] FasesFinales = [FasesDeEstado.Cerrada, FasesDeEstado.Eliminada];

    /// <summary>Una orden está activa mientras su estado actual no sea de una fase final (cerrada o eliminada).</summary>
    public static readonly Expression<Func<OrdenTrabajo, bool>> EsActiva = orden =>
        !FasesFinales.Contains(
            orden.HistorialEstadoOts.OrderByDescending(h => h.FechaHora).ThenByDescending(h => h.IdHEstadoOt)
                .Select(h => h.IdEstadoOtNavigation.Fase).FirstOrDefault() ?? "");

    private readonly SotraContext _contexto;
    private readonly ReservasDeRepuestos _reservas;
    private ReglasDeEstadoOrden? _reglas;

    public OrdenTrabajoDAO(SotraContext contexto)
    {
        _contexto = contexto;
        _reservas = new ReservasDeRepuestos(contexto);
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
            consulta = consulta.Where(o => o.HistorialEstadoOts
                .OrderByDescending(h => h.FechaHora).ThenByDescending(h => h.IdHEstadoOt)
                .Select(h => h.IdEstadoOtNavigation.Nombre).FirstOrDefault() == estado);
        }

        if (!string.IsNullOrEmpty(filtro.Fase))
        {
            var fase = filtro.Fase;
            consulta = consulta.Where(o => o.HistorialEstadoOts
                .OrderByDescending(h => h.FechaHora).ThenByDescending(h => h.IdHEstadoOt)
                .Select(h => h.IdEstadoOtNavigation.Fase).FirstOrDefault() == fase);
        }

        if (filtro.SoloActivas == true) consulta = consulta.Where(EsActiva);

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
                    .Select(h => h.IdEstadoOtNavigation.Nombre).FirstOrDefault() ?? ""))
            .ToListAsync(cancelacion);
    }

    /// <summary>Cuántas órdenes filtradas usaron cada repuesto, agrupado en la base.</summary>
    public async Task<List<RepuestoUsadoEnOrdenes>> ListarRepuestosUsadosAsync(FiltroOrdenesTrabajo filtro, CancellationToken cancelacion)
    {
        return await Filtrar(filtro)
            .SelectMany(o => o.OtRepuestos.Where(r => r.Estado != EstadosRepuestoOrden.Liberado))
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
    public async Task<OrdenTrabajo> CrearAsync(OrdenTrabajo orden, DateOnly fechaPrevista, List<RepuestoPedido> repuestos,
        List<string> sugerencias, Guid? sesion, long? idResponsable, CancellationToken cancelacion)
    {
        await ValidarRelacionesAsync(orden.IdCliente, orden.IdActivo, orden.IdUsuarioTecnico, cancelacion);
        var reglas = await ObtenerReglasAsync(cancelacion);
        orden.IdUsuarioResponsable = idResponsable ?? await ObtenerResponsableAsync(cancelacion);
        orden.FechaCreada = DateTime.UtcNow;
        orden.FechaPrevistaEjecucion = fechaPrevista;
        var lecturaInicial = orden.LecturaUso;
        orden.LecturaUso = null;
        await ValidarCausaDeFallaAsync(orden, cancelacion);

        await using var transaccion = await _contexto.Database.BeginTransactionAsync(cancelacion);
        _contexto.OrdenTrabajos.Add(orden);
        await _contexto.SaveChangesAsync(cancelacion);

        await AgregarEstadoAsync(orden, reglas.Inicial.Nombre, null, null, idResponsable, reglas, cancelacion);
        await AplicarLecturaDeUsoAsync(orden, lecturaInicial, idResponsable, cancelacion);
        RegistrarCambioDeTecnico(orden, null, orden.IdUsuarioTecnico, idResponsable);
        await _reservas.SincronizarAsync(orden, repuestos, false, idResponsable, cancelacion);
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
        List<RepuestoPedido> repuestos, List<string> sugerencias, string estado, string? motivo, Guid? sesion,
        Func<OrdenTrabajo, bool>? coincideConVersionVigente, long? usuarioId, CancellationToken cancelacion)
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

        var reglas = await ObtenerReglasAsync(cancelacion);
        var estadoActual = ObtenerEstadoActual(orden, reglas);
        if (reglas.EstaCerrada(estadoActual))
        {
            throw new ErrorDeOrdenException(ErrorDeOrdenException.OrdenCerrada);
        }

        if (reglas.EsFinal(estadoActual))
        {
            throw new ErrorDeOrdenException(ErrorDeOrdenException.OrdenNoModificable);
        }

        var tecnicoAnterior = orden.IdUsuarioTecnico;
        var ordenIniciada = orden.FechaInicio is not null;
        orden.IdCliente = cambios.ClienteId;
        orden.IdActivo = cambios.ActivoId;
        orden.IdUsuarioTecnico = cambios.TecnicoId;
        orden.Tipo = cambios.Tipo;
        orden.Prioridad = cambios.Prioridad;
        orden.Descripcion = cambios.Descripcion;
        orden.Resultado = cambios.Resultado;
        orden.FechaPrevistaEjecucion = fechaPrevista;
        if (cambios.TiempoEstimadoHoras is not null)
        {
            orden.TiempoEstimadoHoras = cambios.TiempoEstimadoHoras;
        }

        if (cambios.CausaFallaId is not null)
        {
            orden.IdCausaFalla = cambios.CausaFallaId;
        }

        await ValidarCausaDeFallaAsync(orden, cancelacion);
        if (cambios.LecturaUso is not null)
        {
            await AplicarLecturaDeUsoAsync(orden, cambios.LecturaUso, usuarioId, cancelacion);
        }

        ValidarEstado(reglas, estado);
        reglas.ValidarTransicion(estadoActual, estado, orden.IdUsuarioTecnico, motivo);
        await ValidarRelacionesAsync(orden.IdCliente, orden.IdActivo, orden.IdUsuarioTecnico, cancelacion);

        if (estado != estadoActual)
        {
            await AgregarEstadoAsync(orden, estado, estadoActual, motivo, usuarioId, reglas, cancelacion, cambios.MotivoCodigo);
            AplicarFechasDeEstado(orden, estado, reglas);
        }

        RegistrarCambioDeTecnico(orden, tecnicoAnterior, orden.IdUsuarioTecnico, usuarioId);
        await _reservas.SincronizarAsync(orden, repuestos, ordenIniciada, usuarioId, cancelacion);
        await AplicarEfectosEnRepuestosAsync(orden, estado, estadoActual, reglas, usuarioId, cancelacion);
        await ReemplazarSugerenciasAsync(orden, sugerencias, sesion, cancelacion);
        await _contexto.SaveChangesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);

        return (await SeleccionarAsync(orden.IdOrden, cancelacion))!;
    }

    public async Task<OrdenTrabajo> CambiarEstadoAsync(
        OrdenTrabajo ordenLeida, string estado, string? motivo, bool comoTecnico, long? usuarioId, CancellationToken cancelacion,
        decimal? lecturaUso = null, string? motivoCodigo = null)
    {
        await using var transaccion = await _contexto.Database.BeginTransactionAsync(cancelacion);
        var orden = await BloquearYRecargarAsync(ordenLeida.IdOrden, cancelacion);

        var reglas = await ObtenerReglasAsync(cancelacion);
        var estadoActual = ObtenerEstadoActual(orden, reglas);
        if (reglas.EstaCerrada(estadoActual))
        {
            throw new ErrorDeOrdenException(ErrorDeOrdenException.OrdenCerrada);
        }

        ValidarEstado(reglas, estado);
        if (estado == estadoActual)
        {
            throw new ErrorDeOrdenException(ErrorDeOrdenException.TransicionInvalida);
        }

        reglas.ValidarTransicion(estadoActual, estado, orden.IdUsuarioTecnico, motivo, comoTecnico);
        await AgregarEstadoAsync(orden, estado, estadoActual, motivo, usuarioId, reglas, cancelacion, motivoCodigo);
        if (lecturaUso is not null)
        {
            await AplicarLecturaDeUsoAsync(orden, lecturaUso, usuarioId, cancelacion);
        }

        AplicarFechasDeEstado(orden, estado, reglas);
        await AplicarEfectosEnRepuestosAsync(orden, estado, estadoActual, reglas, usuarioId, cancelacion);
        await _contexto.SaveChangesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);

        return (await SeleccionarAsync(orden.IdOrden, cancelacion))!;
    }

    /// <summary>
    /// Cambia los repuestos y cantidades de una orden sin tocar lo demás (lo usa quien gestiona repuestos sin editar la
    /// orden, como el pañolero). Una orden cerrada o eliminada no se modifica. Devuelve <c>null</c> si no existe.
    /// </summary>
    public async Task<OrdenTrabajo?> ActualizarRepuestosAsync(long idOrden, List<RepuestoPedido> repuestos, long? usuarioId, CancellationToken cancelacion)
    {
        await using var transaccion = await _contexto.Database.BeginTransactionAsync(cancelacion);
        if (!await _contexto.OrdenTrabajos.AsNoTracking().AnyAsync(o => o.IdOrden == idOrden, cancelacion))
        {
            return null;
        }

        var orden = await BloquearYRecargarAsync(idOrden, cancelacion);
        var reglas = await ObtenerReglasAsync(cancelacion);
        var estadoActual = ObtenerEstadoActual(orden, reglas);
        if (reglas.EstaCerrada(estadoActual))
        {
            throw new ErrorDeOrdenException(ErrorDeOrdenException.OrdenCerrada);
        }

        if (reglas.EsFinal(estadoActual))
        {
            throw new ErrorDeOrdenException(ErrorDeOrdenException.OrdenNoModificable);
        }

        await _reservas.SincronizarAsync(orden, repuestos, orden.FechaInicio is not null, usuarioId, cancelacion);
        await _contexto.SaveChangesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);

        return (await SeleccionarAsync(orden.IdOrden, cancelacion))!;
    }

    /// <summary>
    /// Qué pasa con los repuestos al entrar en un estado, según lo que dice la base: el estado que completa la fecha de
    /// inicio entrega lo reservado, el que completa la de cierre descuenta el stock y una fase eliminada libera todo.
    /// </summary>
    private async Task AplicarEfectosEnRepuestosAsync(OrdenTrabajo orden, string estado, string estadoAnterior,
        ReglasDeEstadoOrden reglas, long? usuarioId, CancellationToken cancelacion)
    {
        if (estado == estadoAnterior)
        {
            return;
        }

        switch (reglas.CompletaFecha(estado))
        {
            case "inicio":
                _reservas.Entregar(orden, usuarioId);
                break;
            case "cierre":
                await _reservas.DescontarAsync(orden, usuarioId, cancelacion);
                break;
        }

        if (reglas.FaseDe(estado) == FasesDeEstado.Eliminada)
        {
            _reservas.Liberar(orden, usuarioId);
        }
    }

    public async Task<OrdenTrabajo> AsignarTecnicoAsync(OrdenTrabajo ordenLeida, long tecnicoId, long? usuarioId, CancellationToken cancelacion)
    {
        await using var transaccion = await _contexto.Database.BeginTransactionAsync(cancelacion);
        var orden = await BloquearYRecargarAsync(ordenLeida.IdOrden, cancelacion);

        var reglas = await ObtenerReglasAsync(cancelacion);
        var estadoActual = ObtenerEstadoActual(orden, reglas);
        if (reglas.EsFinal(estadoActual))
        {
            throw new ErrorDeOrdenException(ErrorDeOrdenException.OrdenNoModificable);
        }

        if (orden.IdUsuarioTecnico == tecnicoId)
        {
            throw new ErrorDeOrdenException(ErrorDeOrdenException.TecnicoYaAsignado);
        }

        await ValidarTecnicoAsync(tecnicoId, cancelacion);
        var tecnicoAnterior = orden.IdUsuarioTecnico;
        orden.IdUsuarioTecnico = tecnicoId;
        RegistrarCambioDeTecnico(orden, tecnicoAnterior, tecnicoId, usuarioId);

        var estadoNuevo = reglas.EstadoAlAsignarTecnico(estadoActual);
        if (estadoNuevo is not null && estadoNuevo != estadoActual)
        {
            await AgregarEstadoAsync(orden, estadoNuevo, estadoActual, null, usuarioId, reglas, cancelacion);
        }

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

    /// <summary>
    /// Las reglas de los estados leídas de la base (<c>estado_ot</c> y <c>transicion_estado_ot</c>). Se cargan una vez por
    /// instancia, que vive lo que dura el pedido.
    /// </summary>
    public async Task<ReglasDeEstadoOrden> ObtenerReglasAsync(CancellationToken cancelacion)
    {
        if (_reglas is not null)
        {
            return _reglas;
        }

        var estados = await _contexto.EstadoOts.AsNoTracking()
            .Where(estado => estado.Estado)
            .Select(estado => new
            {
                estado.IdEstadoOt,
                estado.Nombre,
                estado.Descripcion,
                estado.Orden,
                estado.Fase,
                estado.ExigeTecnico,
                estado.CompletaFecha,
                estado.Tono,
                estado.CodigoAlerta,
                estado.EsInicial
            })
            .ToListAsync(cancelacion);
        var transiciones = await _contexto.TransicionEstadoOts.AsNoTracking()
            .Select(t => new TransicionDeEstado(t.IdEstadoOrigenNavigation.Nombre, t.IdEstadoDestinoNavigation.Nombre,
                t.ExigeMotivo, t.PuedeTecnico, t.Accion, t.Principal, t.AlAsignarTecnico))
            .ToListAsync(cancelacion);

        _reglas = new ReglasDeEstadoOrden(
            estados.Select(e => new EstadoDeOrden(e.IdEstadoOt, e.Nombre,
                ReglasDeEstadoOrden.EtiquetaDelEstado(e.Nombre, e.Descripcion), e.Orden, e.Fase, e.ExigeTecnico,
                e.CompletaFecha, e.Tono, e.CodigoAlerta, e.EsInicial)),
            transiciones);
        return _reglas;
    }

    private static void AplicarFechasDeEstado(OrdenTrabajo orden, string estado, ReglasDeEstadoOrden reglas)
    {
        switch (reglas.CompletaFecha(estado))
        {
            case "inicio" when orden.FechaInicio is null:
                orden.FechaInicio = DateTime.UtcNow;
                break;
            case "cierre":
                orden.FechaCierre = DateTime.UtcNow;
                break;
        }
    }

    /// <summary>
    /// Órdenes del rango ubicadas por su fecha prevista de ejecución. Las eliminadas se excluyen salvo que se pidan o que
    /// el filtro de estado las nombre. Devuelve como máximo <paramref name="maximo"/> + 1 filas para detectar el recorte.
    /// </summary>
    public async Task<List<OrdenDeCalendario>> ListarCalendarioAsync(
        FiltroOrdenesTrabajo filtro, DateOnly desde, DateOnly hasta, bool incluirEliminadas, int maximo, CancellationToken cancelacion)
    {
        var consulta = Filtrar(filtro with { FechaDesde = null, FechaHasta = null })
            .Where(o => o.FechaPrevistaEjecucion >= desde && o.FechaPrevistaEjecucion <= hasta);
        if (!incluirEliminadas && string.IsNullOrEmpty(filtro.Estado))
        {
            consulta = consulta.Where(o => (o.HistorialEstadoOts
                .OrderByDescending(h => h.FechaHora).ThenByDescending(h => h.IdHEstadoOt)
                .Select(h => h.IdEstadoOtNavigation.Fase).FirstOrDefault() ?? "") != FasesDeEstado.Eliminada);
        }

        return await consulta
            .OrderBy(o => o.FechaPrevistaEjecucion).ThenBy(o => o.IdOrden)
            .Take(maximo + 1)
            .Select(o => new OrdenDeCalendario(
                o.IdOrden, o.FechaPrevistaEjecucion,
                string.IsNullOrWhiteSpace(o.IdActivoNavigation.Nombre) ? o.IdActivoNavigation.NumSerie : o.IdActivoNavigation.Nombre,
                o.IdClienteNavigation.RazonSocial, o.Tipo, o.Prioridad,
                o.HistorialEstadoOts.OrderByDescending(h => h.FechaHora).ThenByDescending(h => h.IdHEstadoOt)
                    .Select(h => h.IdEstadoOtNavigation.Nombre).FirstOrDefault() ?? "",
                o.HistorialEstadoOts.OrderByDescending(h => h.FechaHora).ThenByDescending(h => h.IdHEstadoOt)
                    .Select(h => h.IdEstadoOtNavigation.Fase).FirstOrDefault() ?? ""))
            .ToListAsync(cancelacion);
    }

    /// <summary>Motivos codificados de pausa y devolución, en el orden del catálogo.</summary>
    public async Task<List<MotivoEstadoOt>> ListarMotivosAsync(CancellationToken cancelacion) =>
        await _contexto.MotivoEstadoOts.AsNoTracking()
            .Where(motivo => motivo.Codigo != null)
            .OrderBy(motivo => motivo.IdMotivoEs)
            .ToListAsync(cancelacion);

    private IQueryable<OrdenTrabajo> ConsultaCompleta()
    {
        return _contexto.OrdenTrabajos
            .Include(orden => orden.IdClienteNavigation)
            .Include(orden => orden.IdActivoNavigation)
            .Include(orden => orden.IdUsuarioTecnicoNavigation)
            .Include(orden => orden.IdUsuarioResponsableNavigation)
            .Include(orden => orden.HistorialEstadoOts)
                .ThenInclude(historial => historial.IdEstadoOtNavigation)
            .Include(orden => orden.HistorialEstadoOts)
                .ThenInclude(historial => historial.IdEstadoAnteriorNavigation)
            .Include(orden => orden.HistorialEstadoOts)
                .ThenInclude(historial => historial.IdUsuarioNavigation)
            .Include(orden => orden.HistorialTecnicoOts)
                .ThenInclude(historial => historial.IdTecnicoAnteriorNavigation)
            .Include(orden => orden.HistorialTecnicoOts)
                .ThenInclude(historial => historial.IdTecnicoNuevoNavigation)
            .Include(orden => orden.HistorialTecnicoOts)
                .ThenInclude(historial => historial.IdUsuarioNavigation)
            .Include(orden => orden.IdCausaFallaNavigation)
            .Include(orden => orden.HistorialLecturaUsoOts)
                .ThenInclude(historial => historial.IdUsuarioNavigation)
            .Include(orden => orden.HistorialRecomendacionesIa)
            .Include(orden => orden.OtRepuestos)
                .ThenInclude(relacion => relacion.IdRepuestoNavigation)
            .Include(orden => orden.HistorialRepuestoOts)
                .ThenInclude(historial => historial.IdRepuestoNavigation)
            .Include(orden => orden.HistorialRepuestoOts)
                .ThenInclude(historial => historial.IdUsuarioNavigation)
            .AsSplitQuery();
    }

    private async Task ValidarRelacionesAsync(
        long clienteId, long activoId, long? tecnicoId, CancellationToken cancelacion)
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

    /// <summary>La causa de falla, si se indica, debe existir, estar activa y corresponder a una correctiva.</summary>
    private async Task ValidarCausaDeFallaAsync(OrdenTrabajo orden, CancellationToken cancelacion)
    {
        if (orden.IdCausaFalla is null)
        {
            return;
        }

        if (orden.Tipo)
        {
            throw new ValidacionDeNegocioException("La causa de falla solo se indica en órdenes correctivas.", "CAUSA_FALLA_SOLO_CORRECTIVA");
        }

        var causa = orden.IdCausaFalla.Value;
        if (!await _contexto.CausasFalla.AsNoTracking().AnyAsync(item => item.IdCausaFalla == causa && item.Estado, cancelacion))
        {
            throw new ValidacionDeNegocioException("La causa de falla indicada no existe o está inactiva.", "CAUSA_FALLA_INVALIDA");
        }
    }

    /// <summary>
    /// Guarda la lectura de uso (horas o kilómetros del activo) y, si cambió, registra el valor anterior, el nuevo,
    /// quién lo hizo y cuándo. La lectura no puede ser menor que la de una orden anterior del mismo activo ni mayor
    /// que la de una posterior.
    /// </summary>
    private async Task AplicarLecturaDeUsoAsync(OrdenTrabajo orden, decimal? nueva, long? usuarioId, CancellationToken cancelacion)
    {
        if (nueva is null || nueva == orden.LecturaUso)
        {
            return;
        }

        var otras = _contexto.OrdenTrabajos.AsNoTracking()
            .Where(item => item.IdActivo == orden.IdActivo && item.IdOrden != orden.IdOrden && item.LecturaUso != null);
        var maximaAnterior = await otras.Where(item => item.FechaCreada < orden.FechaCreada)
            .MaxAsync(item => item.LecturaUso, cancelacion);
        var minimaPosterior = await otras.Where(item => item.FechaCreada > orden.FechaCreada)
            .MinAsync(item => item.LecturaUso, cancelacion);
        if (nueva < maximaAnterior || nueva > minimaPosterior)
        {
            throw new ValidacionDeNegocioException(
                "La lectura de uso no puede ser menor que la de una orden anterior del activo ni mayor que la de una posterior.",
                "LECTURA_USO_INVALIDA");
        }

        orden.HistorialLecturaUsoOts.Add(new HistorialLecturaUsoOt
        {
            LecturaAnterior = orden.LecturaUso,
            LecturaNueva = nueva,
            IdUsuario = usuarioId,
            FechaHora = DateTime.UtcNow
        });
        orden.LecturaUso = nueva;
    }

    /// <summary>Guarda un cambio de estado en el historial: de qué estado a cuál, cuándo, quién lo hizo y el motivo.</summary>
    private async Task AgregarEstadoAsync(OrdenTrabajo orden, string nombreEstado, string? estadoAnterior,
        string? observacion, long? usuarioId, ReglasDeEstadoOrden reglas, CancellationToken cancelacion,
        string? motivoCodigo = null)
    {
        var estado = await _contexto.EstadoOts.FirstOrDefaultAsync(item => item.Nombre == nombreEstado && item.Estado, cancelacion)
            ?? throw new ValidacionDeNegocioException("El estado indicado no es válido.");

        long? idMotivo = null;
        if (!string.IsNullOrWhiteSpace(motivoCodigo))
        {
            var codigo = motivoCodigo.Trim();
            idMotivo = await _contexto.MotivoEstadoOts.AsNoTracking()
                .Where(motivo => motivo.Codigo == codigo)
                .Select(motivo => (long?)motivo.IdMotivoEs)
                .FirstOrDefaultAsync(cancelacion)
                ?? throw new ValidacionDeNegocioException("El motivo indicado no existe.", "MOTIVO_INVALIDO");
        }

        orden.HistorialEstadoOts.Add(new HistorialEstadoOt
        {
            IdMotivoEs = idMotivo,
            IdEstadoOtNavigation = estado,
            IdEstadoAnterior = estadoAnterior is null ? null : reglas.Buscar(estadoAnterior)?.Id,
            IdUsuario = usuarioId,
            FechaHora = DateTime.UtcNow,
            Observacion = observacion
        });
    }

    /// <summary>Guarda una asignación o un cambio de técnico; no hace nada si el técnico es el mismo.</summary>
    private static void RegistrarCambioDeTecnico(OrdenTrabajo orden, long? tecnicoAnterior, long? tecnicoNuevo, long? usuarioId)
    {
        if (tecnicoNuevo is null || tecnicoNuevo == tecnicoAnterior)
        {
            return;
        }

        orden.HistorialTecnicoOts.Add(new HistorialTecnicoOt
        {
            IdTecnicoAnterior = tecnicoAnterior,
            IdTecnicoNuevo = tecnicoNuevo,
            IdUsuario = usuarioId,
            FechaHora = DateTime.UtcNow
        });
    }

    private static void ValidarEstado(ReglasDeEstadoOrden reglas, string estado)
    {
        if (!reglas.EsValido(estado))
        {
            throw new ValidacionDeNegocioException("El estado indicado no es válido.");
        }
    }

    private static string ObtenerEstadoActual(OrdenTrabajo orden, ReglasDeEstadoOrden reglas)
    {
        var ultimoEstado = orden.HistorialEstadoOts
            .OrderByDescending(item => item.FechaHora)
            .ThenByDescending(item => item.IdHEstadoOt)
            .FirstOrDefault();
        if (ultimoEstado is null)
        {
            return reglas.Inicial.Nombre;
        }

        return ultimoEstado.IdEstadoOtNavigation.Nombre;
    }

    public async Task RegistrarSugerenciaGeneradaAsync(
        Guid? sesion, string consulta, int numeroIntento, string texto, string modo, CancellationToken cancelacion)
    {
        _contexto.HistorialRecomendacionesIa.Add(new HistorialRecomendacionIa
        {
            Recomendacion = texto.Trim(),
            Estado = false,
            Sesion = sesion,
            Consulta = consulta,
            NumeroIntento = numeroIntento,
            FechaGeneracion = DateTime.UtcNow,
            Resultado = "pendiente",
            Modo = modo
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
