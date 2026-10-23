using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones;

public class MovimientoStockDAO
{
    private readonly SotraContext _contexto;

    public MovimientoStockDAO(SotraContext contexto)
    {
        _contexto = contexto;
    }

    public async Task<List<MovimientoStock>> ListarTodoAsync(CancellationToken cancelacion)
    {
        return await _contexto.MovimientoStocks
            .AsNoTracking()
            .Include(movimiento => movimiento.IdRepuestoNavigation)
            .Include(movimiento => movimiento.IdUsuarioNavigation)
            .OrderByDescending(movimiento => movimiento.Fecha)
            .ThenByDescending(movimiento => movimiento.IdMovimiento)
            .ToListAsync(cancelacion);
    }

    public async Task<(List<MovimientoStock> Elementos, int Total)> ListarPaginaAsync(
        FiltroMovimientosStock filtro, int pagina, int tamanoPagina, CancellationToken cancelacion)
    {
        var consulta = _contexto.MovimientoStocks.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Buscar))
        {
            var patron = ConsultasDeTexto.PatronContiene(filtro.Buscar);
            consulta = consulta.Where(m => EF.Functions.ILike(m.IdRepuestoNavigation.Codigo, patron)
                                           || EF.Functions.ILike(m.IdRepuestoNavigation.Nombre, patron)
                                           || (m.Motivo != null && EF.Functions.ILike(m.Motivo, patron)));
        }

        if (filtro.RepuestoId is { } repuestoId)
        {
            consulta = consulta.Where(m => m.IdRepuesto == repuestoId);
        }

        if (!string.IsNullOrEmpty(filtro.Tipo))
        {
            consulta = consulta.Where(m => m.Tipo == filtro.Tipo);
        }

        if (filtro.FechaDesde is { } desde)
        {
            var inicio = desde.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            consulta = consulta.Where(m => m.Fecha >= inicio);
        }

        if (filtro.FechaHasta is { } hasta)
        {
            var finExclusivo = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            consulta = consulta.Where(m => m.Fecha < finExclusivo);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Ubicacion))
        {
            var patronUbicacion = ConsultasDeTexto.PatronContiene(filtro.Ubicacion);
            consulta = consulta.Where(m => m.Ubicacion != null && EF.Functions.ILike(m.Ubicacion, patronUbicacion));
        }

        if (filtro.UsuarioId is { } usuarioId)
        {
            consulta = consulta.Where(m => m.IdUsuario == usuarioId);
        }

        var ordenada = consulta
            .Include(m => m.IdRepuestoNavigation)
            .Include(m => m.IdUsuarioNavigation)
            .OrderByDescending(m => m.Fecha)
            .ThenByDescending(m => m.IdMovimiento);

        return await ConsultasDeTexto.PaginarAsync(ordenada, pagina, tamanoPagina, cancelacion);
    }

    public async Task<MovimientoStock?> SeleccionarAsync(long id, CancellationToken cancelacion)
    {
        return await _contexto.MovimientoStocks
            .AsNoTracking()
            .Include(movimiento => movimiento.IdRepuestoNavigation)
            .Include(movimiento => movimiento.IdUsuarioNavigation)
            .FirstOrDefaultAsync(movimiento => movimiento.IdMovimiento == id, cancelacion);
    }

    /// <summary>
    /// Aplica un movimiento de stock de forma atómica: bloquea la fila del repuesto, valida y registra el
    /// movimiento con el stock anterior y posterior. El bloqueo evita que dos movimientos simultáneos se pisen.
    /// </summary>
    public async Task<ResultadoMovimientoStock> RegistrarAsync(SolicitudMovimientoStock solicitud, CancellationToken cancelacion)
    {
        await using var transaccion = await _contexto.Database.BeginTransactionAsync(cancelacion);

        var repuesto = await _contexto.Repuestos
            .FromSqlInterpolated($"SELECT * FROM operativo.repuesto WHERE id_repuesto = {solicitud.RepuestoId} FOR UPDATE")
            .AsNoTracking()
            .FirstOrDefaultAsync(cancelacion);

        if (repuesto is null)
        {
            return ResultadoMovimientoStock.Fallo(EstadoMovimientoStock.RepuestoNoEncontrado);
        }

        if (!repuesto.Estado)
        {
            return ResultadoMovimientoStock.Fallo(EstadoMovimientoStock.RepuestoInactivo);
        }

        var stockAnterior = repuesto.Cantidad;
        var stockPosterior = solicitud.Tipo switch
        {
            TiposMovimientoStock.Ingreso => (long)stockAnterior + solicitud.Cantidad,
            TiposMovimientoStock.Egreso => (long)stockAnterior - solicitud.Cantidad,
            _ => solicitud.Cantidad,
        };

        if (stockPosterior < 0)
        {
            return ResultadoMovimientoStock.Fallo(EstadoMovimientoStock.StockInsuficiente);
        }

        if (stockPosterior > int.MaxValue)
        {
            return ResultadoMovimientoStock.Fallo(EstadoMovimientoStock.StockExcedido);
        }

        await _contexto.Repuestos
            .Where(r => r.IdRepuesto == solicitud.RepuestoId)
            .ExecuteUpdateAsync(cambios => cambios.SetProperty(r => r.Cantidad, (int)stockPosterior), cancelacion);

        var movimiento = new MovimientoStock
        {
            Estado = true,
            Tipo = solicitud.Tipo,
            Fecha = solicitud.Fecha,
            Cantidad = solicitud.Tipo == TiposMovimientoStock.Ajuste
                ? (int)stockPosterior - stockAnterior
                : solicitud.Cantidad,
            IdRepuesto = solicitud.RepuestoId,
            IdUsuario = solicitud.IdUsuario,
            Motivo = solicitud.Motivo,
            Ubicacion = solicitud.Ubicacion ?? repuesto.Ubicacion,
            Observaciones = solicitud.Observaciones,
            StockAnterior = stockAnterior,
            StockPosterior = (int)stockPosterior,
        };

        _contexto.MovimientoStocks.Add(movimiento);
        await _contexto.SaveChangesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);

        return ResultadoMovimientoStock.Exito((await SeleccionarAsync(movimiento.IdMovimiento, cancelacion))!);
    }
}

public static class TiposMovimientoStock
{
    public const string Ingreso = "ingreso";
    public const string Egreso = "egreso";
    public const string Ajuste = "ajuste";
}

/// <param name="Cantidad">Unidades a sumar o restar; en un ajuste es el stock real contado.</param>
/// <param name="IdUsuario">Quién registra el movimiento; null si el pedido no trae token (AUTH_OBLIGATORIA=false).</param>
public sealed record SolicitudMovimientoStock(
    long RepuestoId, string Tipo, int Cantidad, DateTime Fecha, string Motivo, string? Ubicacion, string? Observaciones,
    long? IdUsuario = null);

public enum EstadoMovimientoStock
{
    Registrado,
    RepuestoNoEncontrado,
    RepuestoInactivo,
    StockInsuficiente,
    StockExcedido,
}

public sealed record ResultadoMovimientoStock(EstadoMovimientoStock Estado, MovimientoStock? Movimiento)
{
    public static ResultadoMovimientoStock Exito(MovimientoStock movimiento) => new(EstadoMovimientoStock.Registrado, movimiento);

    public static ResultadoMovimientoStock Fallo(EstadoMovimientoStock estado) => new(estado, null);
}
