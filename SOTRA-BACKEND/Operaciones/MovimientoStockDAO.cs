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
        string ubicacion, string? observaciones, long? idUsuario)
    {
        return Registrar(repuesto, "ingreso", cantidad, repuesto.Cantidad + cantidad, fecha, motivo, ubicacion,
            observaciones, idUsuario);
    }

    // El controlador ya verificó que la cantidad no supera el stock disponible.
    public MovimientoStock RegistrarEgreso(Repuesto repuesto, int cantidad, DateTime fecha, string motivo,
        string ubicacion, string? observaciones, long? idUsuario)
    {
        return Registrar(repuesto, "egreso", cantidad, repuesto.Cantidad - cantidad, fecha, motivo, ubicacion,
            observaciones, idUsuario);
    }

    // El ajuste fija el stock real contado; la cantidad del movimiento es la diferencia (en valor absoluto).
    public MovimientoStock RegistrarAjuste(Repuesto repuesto, int cantidadReal, DateTime fecha, string motivo,
        string ubicacion, string? observaciones, long? idUsuario)
    {
        return Registrar(repuesto, "ajuste", Math.Abs(cantidadReal - repuesto.Cantidad), cantidadReal, fecha, motivo,
            ubicacion, observaciones, idUsuario);
    }

    private MovimientoStock Registrar(Repuesto repuesto, string tipo, int cantidad, int stockPosterior, DateTime fecha,
        string motivo, string ubicacion, string? observaciones, long? idUsuario)
    {
        using var transaccion = _contexto.Database.BeginTransaction();

        var stockAnterior = repuesto.Cantidad;
        repuesto.Cantidad = stockPosterior;

        var movimiento = new MovimientoStock
        {
            Estado = true,
            Tipo = tipo,
            Fecha = fecha,
            Cantidad = cantidad,
            IdRepuesto = repuesto.IdRepuesto,
            IdUsuario = idUsuario,
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
