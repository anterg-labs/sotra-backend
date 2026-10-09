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
