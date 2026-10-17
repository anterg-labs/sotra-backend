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

    public List<MovimientoStock> ListarTodo()
    {
        return _contexto.MovimientoStocks
            .AsNoTracking()
            .Include(movimiento => movimiento.IdRepuestoNavigation)
            .Include(movimiento => movimiento.IdUsuarioNavigation)
            .OrderByDescending(movimiento => movimiento.Fecha)
            .ThenByDescending(movimiento => movimiento.IdMovimiento)
            .ToList();
    }

    public (List<MovimientoStock> Elementos, int Total) ListarPagina(
        FiltroMovimientosStock filtro, int pagina, int tamanoPagina)
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

        return ConsultasDeTexto.Paginar(ordenada, pagina, tamanoPagina);
    }

    public MovimientoStock? Seleccionar(long id)
    {
        return _contexto.MovimientoStocks
            .AsNoTracking()
            .Include(movimiento => movimiento.IdRepuestoNavigation)
            .Include(movimiento => movimiento.IdUsuarioNavigation)
            .FirstOrDefault(movimiento => movimiento.IdMovimiento == id);
    }

    public MovimientoStock RegistrarIngreso(Repuesto repuesto, int cantidad, DateTime fecha, string motivo,
        string ubicacion, string? observaciones)
    {
        using var transaccion = _contexto.Database.BeginTransaction();

        var stockAnterior = repuesto.Cantidad;
        var stockPosterior = stockAnterior + cantidad;
        repuesto.Cantidad = stockPosterior;

        var movimiento = new MovimientoStock
        {
            Estado = true,
            Tipo = "ingreso",
            Fecha = fecha,
            Cantidad = cantidad,
            IdRepuesto = repuesto.IdRepuesto,
            Motivo = motivo,
            Ubicacion = ubicacion,
            Observaciones = observaciones,
            StockAnterior = stockAnterior,
            StockPosterior = stockPosterior
        };

        _contexto.MovimientoStocks.Add(movimiento);
        _contexto.SaveChanges();
        transaccion.Commit();

        return Seleccionar(movimiento.IdMovimiento)!;
    }
}
