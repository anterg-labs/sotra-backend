using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones;

public class RepuestoDAO
{
    public static readonly ColumnasOrdenables<Repuesto> ColumnasOrdenables = new(
        r => r.IdRepuesto,
        [
            new("codigo", r => r.Codigo),
            new("nombre", r => r.Nombre),
            new("marca", r => r.Marca),
            new("categoria", r => r.Categoria),
            new("unidadMedida", r => r.UnidadMedida),
            new("ubicacion", r => r.Ubicacion),
            new("cantidad", r => r.Cantidad),
            new("cantidadMinima", r => r.CantMin),
            new("cantidadReservada", r => r.OtRepuestos.Where(x => x.Estado == EstadosRepuestoOrden.Reservado).Sum(x => x.Cantidad)),
            new("cantidadEntregada", r => r.OtRepuestos.Where(x => x.Estado == EstadosRepuestoOrden.Entregado).Sum(x => x.Cantidad)),
            new("cantidadDisponible", r => r.Cantidad - r.OtRepuestos.Where(x => EstadosRepuestoOrden.Comprometidos.Contains(x.Estado)).Sum(x => x.Cantidad)),
            new("estado", r => r.Estado),
        ]);

    private readonly SotraContext _contexto;

    public RepuestoDAO(SotraContext contexto)
    {
        _contexto = contexto;
    }


    public async Task<List<Repuesto>> ListarTodoAsync(CancellationToken cancelacion)
    {
        return await _contexto.Repuestos
            .OrderBy(repuesto => repuesto.IdRepuesto)
            .ToListAsync(cancelacion);
    }

    public async Task<(List<Repuesto> Elementos, int Total)> ListarPaginaAsync(
        FiltroRepuestos filtro, int pagina, int tamanoPagina, CancellationToken cancelacion)
    {
        var consulta = _contexto.Repuestos.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Buscar))
        {
            var patron = ConsultasDeTexto.PatronContiene(filtro.Buscar);
            consulta = consulta.Where(r => EF.Functions.ILike(r.Codigo, patron)
                                           || EF.Functions.ILike(r.Nombre, patron)
                                           || (r.Marca != null && EF.Functions.ILike(r.Marca, patron))
                                           || (r.Categoria != null && EF.Functions.ILike(r.Categoria, patron))
                                           || (r.ModeloCompatibilidad != null && EF.Functions.ILike(r.ModeloCompatibilidad, patron))
                                           || (r.Descripcion != null && EF.Functions.ILike(r.Descripcion, patron)));
        }

        if (filtro.Estado is { } estado)
        {
            consulta = consulta.Where(r => r.Estado == estado);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Ubicacion))
        {
            var patronUbicacion = ConsultasDeTexto.PatronContiene(filtro.Ubicacion);
            consulta = consulta.Where(r => r.Ubicacion != null && EF.Functions.ILike(r.Ubicacion, patronUbicacion));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Categoria))
        {
            var patronCategoria = ConsultasDeTexto.PatronContiene(filtro.Categoria);
            consulta = consulta.Where(r => r.Categoria != null && EF.Functions.ILike(r.Categoria, patronCategoria));
        }

        if (filtro.BajoStock == true)
        {
            consulta = consulta.Where(r => r.Cantidad <= 0 || (r.CantMin > 0 && r.Cantidad <= r.CantMin));
        }

        consulta = filtro.EstadoStock switch
        {
            EstadosStock.SinStock => consulta.Where(r => r.Cantidad <= 0),
            EstadosStock.Bajo => consulta.Where(r => r.Cantidad > 0 && r.CantMin > 0 && r.Cantidad <= r.CantMin),
            EstadosStock.Normal => consulta.Where(r => r.Cantidad > 0 && !(r.CantMin > 0 && r.Cantidad <= r.CantMin)),
            _ => consulta,
        };

        var ordenada = !string.IsNullOrEmpty(filtro.OrdenarPor)
            ? ColumnasOrdenables.Aplicar(consulta, filtro.OrdenarPor, filtro.Descendente)
            : filtro.OrdenarPorCriticidad
                ? consulta.OrderBy(r => r.Cantidad <= 0 ? 0 : (r.CantMin > 0 && r.Cantidad <= r.CantMin ? 1 : 2))
                    .ThenBy(r => r.IdRepuesto)
                : consulta.OrderBy(r => r.IdRepuesto);

        return await ConsultasDeTexto.PaginarAsync(ordenada, pagina, tamanoPagina, cancelacion);
    }

    /// <summary>Repuestos activos con stock bajo o sin stock, filtrados en la base.</summary>
    public async Task<List<Repuesto>> ListarConStockInsuficienteAsync(CancellationToken cancelacion)
    {
        return await _contexto.Repuestos.AsNoTracking()
            .Where(r => r.Estado && (r.Cantidad <= 0 || (r.CantMin > 0 && r.Cantidad <= r.CantMin)))
            .OrderBy(r => r.IdRepuesto)
            .ToListAsync(cancelacion);
    }

    /// <summary>Cuenta en la base los repuestos activos con stock bajo o sin stock.</summary>
    public async Task<AlertasStock> ContarAlertasAsync(CancellationToken cancelacion)
    {
        var activos = _contexto.Repuestos.AsNoTracking().Where(r => r.Estado);
        var sinStock = await activos.CountAsync(r => r.Cantidad <= 0, cancelacion);
        var bajo = await activos.CountAsync(r => r.Cantidad > 0 && r.CantMin > 0 && r.Cantidad <= r.CantMin, cancelacion);
        return new AlertasStock(bajo, sinStock);
    }

    public async Task<Repuesto?> ActualizarMinimoAsync(long id, int cantidadMinima, CancellationToken cancelacion)
    {
        var filas = await _contexto.Repuestos
            .Where(r => r.IdRepuesto == id)
            .ExecuteUpdateAsync(cambios => cambios.SetProperty(r => r.CantMin, cantidadMinima), cancelacion);
        return filas == 0 ? null : await _contexto.Repuestos.AsNoTracking().FirstAsync(r => r.IdRepuesto == id, cancelacion);
    }

    /// <summary>Unidades reservadas y entregadas a órdenes abiertas de cada repuesto, agrupadas en la base.</summary>
    public Task<Dictionary<long, StockComprometido>> ObtenerComprometidoAsync(IReadOnlyCollection<long> ids, CancellationToken cancelacion) =>
        new ReservasDeRepuestos(_contexto).ObtenerComprometidoAsync(ids, null, cancelacion);

    /// <summary>
    /// Órdenes que hoy tienen unidades del repuesto reservadas o entregadas (las que restan del disponible), con el
    /// momento en que la línea llegó a ese estado. Con <paramref name="estado"/> se limita a <c>reservado</c> o
    /// <c>entregado</c>. Primero las entregadas y, dentro de cada estado, por número de orden.
    /// </summary>
    public async Task<List<OrdenConRepuestoComprometido>> ListarOrdenesComprometidasAsync(long repuestoId, string? estado,
        CancellationToken cancelacion)
    {
        var estados = estado is null ? EstadosRepuestoOrden.Comprometidos : [estado];
        return await _contexto.OtRepuestos.AsNoTracking()
            .Where(fila => fila.IdRepuesto == repuestoId && estados.Contains(fila.Estado))
            .OrderBy(fila => fila.Estado == EstadosRepuestoOrden.Entregado ? 0 : 1)
            .ThenBy(fila => fila.IdOrden)
            .Select(fila => new OrdenConRepuestoComprometido(
                fila.IdOrden,
                fila.Estado,
                fila.Cantidad,
                fila.IdOrdenNavigation.HistorialEstadoOts
                    .OrderByDescending(h => h.FechaHora).ThenByDescending(h => h.IdHEstadoOt)
                    .Select(h => h.IdEstadoOtNavigation.Nombre).FirstOrDefault() ?? "",
                fila.IdOrdenNavigation.IdClienteNavigation.RazonSocial,
                string.IsNullOrWhiteSpace(fila.IdOrdenNavigation.IdActivoNavigation.Nombre)
                    ? fila.IdOrdenNavigation.IdActivoNavigation.NumSerie
                    : fila.IdOrdenNavigation.IdActivoNavigation.Nombre,
                fila.IdOrdenNavigation.IdUsuarioTecnicoNavigation == null
                    ? null
                    : fila.IdOrdenNavigation.IdUsuarioTecnicoNavigation.Nombre + " " + fila.IdOrdenNavigation.IdUsuarioTecnicoNavigation.Apellido,
                fila.IdOrdenNavigation.FechaPrevistaEjecucion,
                fila.IdOrdenNavigation.HistorialRepuestoOts
                    .Where(h => h.IdRepuesto == repuestoId && h.EstadoNuevo == fila.Estado)
                    .Max(h => (DateTime?)h.FechaHora)))
            .ToListAsync(cancelacion);
    }

    public async Task<bool> ExisteAsync(long id, CancellationToken cancelacion) =>
        await _contexto.Repuestos.AnyAsync(repuesto => repuesto.IdRepuesto == id, cancelacion);

    public async Task<Repuesto?> SeleccionarAsync(long id, CancellationToken cancelacion)
    {
        return await _contexto.Repuestos.FirstOrDefaultAsync(repuesto => repuesto.IdRepuesto == id, cancelacion);
    }

    public async Task<Repuesto> InsertarAsync(Repuesto repuesto, CancellationToken cancelacion)
    {
        _contexto.Repuestos.Add(repuesto);
        await _contexto.SaveChangesAsync(cancelacion);
        return repuesto;
    }

    public async Task ActualizarAsync(Repuesto repuesto, CancellationToken cancelacion)
    {
        await _contexto.SaveChangesAsync(cancelacion);
    }

    public Task<bool> DesactivarAsync(long id, CancellationToken cancelacion) => CambiarEstadoAsync(id, false, cancelacion);

    public Task<bool> ReactivarAsync(long id, CancellationToken cancelacion) => CambiarEstadoAsync(id, true, cancelacion);

    private async Task<bool> CambiarEstadoAsync(long id, bool estado, CancellationToken cancelacion)
    {
        return await _contexto.Repuestos
            .Where(r => r.IdRepuesto == id)
            .ExecuteUpdateAsync(cambios => cambios.SetProperty(r => r.Estado, estado), cancelacion) > 0;
    }
}

/// <summary>Una orden que tiene unidades de un repuesto reservadas o entregadas.</summary>
/// <param name="EstadoRepuesto"><c>reservado</c> o <c>entregado</c>.</param>
/// <param name="Desde">Cuándo la línea pasó a ese estado según el historial; <c>null</c> si no quedó registrado.</param>
public sealed record OrdenConRepuestoComprometido(
    long IdOrden,
    string EstadoRepuesto,
    int Cantidad,
    string EstadoOrden,
    string NombreCliente,
    string NombreActivo,
    string? NombreTecnico,
    DateOnly FechaPrevistaEjecucion,
    DateTime? Desde);
