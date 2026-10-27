using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones;

/// <summary>
/// Ciclo de un repuesto dentro de una orden: <c>reservado</c> al asignarlo, <c>entregado</c> cuando la orden empieza,
/// <c>descontado</c> al cerrarla (recién ahí baja el stock) y <c>liberado</c> si se quita o la orden se elimina.
/// </summary>
public static class EstadosRepuestoOrden
{
    public const string Reservado = "reservado";
    public const string Entregado = "entregado";
    public const string Descontado = "descontado";
    public const string Liberado = "liberado";

    /// <summary>Las unidades que ninguna otra orden puede usar mientras estén en uno de estos estados.</summary>
    public static readonly string[] Comprometidos = [Reservado, Entregado];
}

public static class AccionesRepuestoOrden
{
    public const string Reservar = "reservar";
    public const string Modificar = "modificar";
    public const string Entregar = "entregar";
    public const string Descontar = "descontar";
    public const string Liberar = "liberar";
}

/// <summary>Un repuesto con la cantidad que se pide para una orden.</summary>
public sealed record RepuestoPedido(long RepuestoId, int Cantidad);

/// <summary>Unidades de un repuesto que están reservadas o entregadas a órdenes abiertas.</summary>
public sealed record StockComprometido(int Reservado, int Entregado)
{
    public static readonly StockComprometido Ninguno = new(0, 0);

    public int Total => Reservado + Entregado;
}

/// <summary>
/// Reglas de reserva, entrega y descuento de repuestos de una orden. Todos los métodos asumen una transacción abierta
/// del que llama y bloquean las filas de los repuestos involucrados (en orden de id, para no generar bloqueos cruzados),
/// de modo que dos órdenes no pueden reservar las mismas unidades a la vez. Cada cambio se guarda en el historial con
/// quién lo hizo y cuándo.
/// </summary>
public sealed class ReservasDeRepuestos(SotraContext contexto)
{
    public const string StockInsuficiente = "STOCK_INSUFICIENTE";
    public const string StockReservado = "STOCK_RESERVADO";
    public const string StockEntregado = "STOCK_ENTREGADO";

    private const int CantidadMaxima = 1_000_000;

    /// <summary>
    /// Deja los repuestos activos de la orden iguales a <paramref name="pedidos"/>: reserva lo nuevo, ajusta cantidades y
    /// libera lo que ya no está. Lo que se agrega a una orden que ya empezó queda entregado al instante. Si no alcanza el
    /// stock disponible, falla con <c>409</c> indicando si falta stock, o si las unidades están reservadas o entregadas.
    /// </summary>
    public async Task SincronizarAsync(OrdenTrabajo orden, IReadOnlyList<RepuestoPedido> pedidos, bool ordenIniciada,
        long? usuarioId, CancellationToken cancelacion)
    {
        ValidarPedidos(pedidos);
        var activas = orden.OtRepuestos.Where(fila => fila.Estado != EstadosRepuestoOrden.Liberado).ToList();
        var ids = pedidos.Select(pedido => pedido.RepuestoId).Union(activas.Select(fila => fila.IdRepuesto)).Distinct().Order().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var repuestos = await BloquearAsync(ids, cancelacion);
        var deOtrasOrdenes = await ObtenerComprometidoAsync(ids, orden.IdOrden, cancelacion);
        var ahora = DateTime.UtcNow;

        foreach (var pedido in pedidos)
        {
            var actual = activas.FirstOrDefault(fila => fila.IdRepuesto == pedido.RepuestoId);
            if (actual is not null && actual.Cantidad == pedido.Cantidad)
            {
                continue;
            }

            if (actual is null || pedido.Cantidad > actual.Cantidad)
            {
                if (!repuestos.TryGetValue(pedido.RepuestoId, out var repuesto) || !repuesto.Estado)
                {
                    throw new ValidacionDeNegocioException("Uno de los repuestos no existe o está inactivo.");
                }

                ExigirDisponible(repuesto, pedido.Cantidad, deOtrasOrdenes.GetValueOrDefault(pedido.RepuestoId, StockComprometido.Ninguno));
            }

            if (actual is null)
            {
                var estado = ordenIniciada ? EstadosRepuestoOrden.Entregado : EstadosRepuestoOrden.Reservado;
                orden.OtRepuestos.Add(new OtRepuesto { IdRepuesto = pedido.RepuestoId, Cantidad = pedido.Cantidad, Estado = estado });
                Registrar(orden, pedido.RepuestoId, ordenIniciada ? AccionesRepuestoOrden.Entregar : AccionesRepuestoOrden.Reservar,
                    null, pedido.Cantidad, null, estado, usuarioId, ahora);
            }
            else
            {
                Registrar(orden, pedido.RepuestoId, AccionesRepuestoOrden.Modificar, actual.Cantidad, pedido.Cantidad,
                    actual.Estado, actual.Estado, usuarioId, ahora);
                actual.Cantidad = pedido.Cantidad;
            }
        }

        var pedidosIds = pedidos.Select(pedido => pedido.RepuestoId).ToHashSet();
        foreach (var fila in activas.Where(fila => !pedidosIds.Contains(fila.IdRepuesto)))
        {
            Registrar(orden, fila.IdRepuesto, AccionesRepuestoOrden.Liberar, fila.Cantidad, fila.Cantidad, fila.Estado,
                EstadosRepuestoOrden.Liberado, usuarioId, ahora);
            fila.Estado = EstadosRepuestoOrden.Liberado;
        }
    }

    /// <summary>La orden empieza: lo reservado pasa a entregado.</summary>
    public void Entregar(OrdenTrabajo orden, long? usuarioId)
    {
        var ahora = DateTime.UtcNow;
        foreach (var fila in orden.OtRepuestos.Where(fila => fila.Estado == EstadosRepuestoOrden.Reservado))
        {
            Registrar(orden, fila.IdRepuesto, AccionesRepuestoOrden.Entregar, fila.Cantidad, fila.Cantidad, fila.Estado,
                EstadosRepuestoOrden.Entregado, usuarioId, ahora);
            fila.Estado = EstadosRepuestoOrden.Entregado;
        }
    }

    /// <summary>La orden se elimina: lo reservado y lo entregado vuelve a estar libre.</summary>
    public void Liberar(OrdenTrabajo orden, long? usuarioId)
    {
        var ahora = DateTime.UtcNow;
        foreach (var fila in orden.OtRepuestos.Where(fila => EstadosRepuestoOrden.Comprometidos.Contains(fila.Estado)))
        {
            Registrar(orden, fila.IdRepuesto, AccionesRepuestoOrden.Liberar, fila.Cantidad, fila.Cantidad, fila.Estado,
                EstadosRepuestoOrden.Liberado, usuarioId, ahora);
            fila.Estado = EstadosRepuestoOrden.Liberado;
        }
    }

    /// <summary>
    /// La orden se cierra: lo entregado se descuenta del stock con un egreso por repuesto (con el usuario que cerró).
    /// Si el stock físico no alcanza, falla y la orden no se cierra.
    /// </summary>
    public async Task DescontarAsync(OrdenTrabajo orden, long? usuarioId, CancellationToken cancelacion)
    {
        var filas = orden.OtRepuestos.Where(fila => EstadosRepuestoOrden.Comprometidos.Contains(fila.Estado)).ToList();
        if (filas.Count == 0)
        {
            return;
        }

        var repuestos = await BloquearAsync(filas.Select(fila => fila.IdRepuesto).Distinct().Order().ToList(), cancelacion);
        var ahora = DateTime.UtcNow;
        foreach (var fila in filas.OrderBy(fila => fila.IdRepuesto))
        {
            var repuesto = repuestos[fila.IdRepuesto];
            if (repuesto.Cantidad < fila.Cantidad)
            {
                throw new ConflictoDeDatosException(StockInsuficiente,
                    $"No se puede cerrar la orden: de {Etiqueta(repuesto)} hay {repuesto.Cantidad} y se descuentan {fila.Cantidad}. Registrá primero el ingreso de stock.");
            }

            var posterior = repuesto.Cantidad - fila.Cantidad;
            await contexto.Repuestos.Where(r => r.IdRepuesto == fila.IdRepuesto)
                .ExecuteUpdateAsync(cambios => cambios.SetProperty(r => r.Cantidad, posterior), cancelacion);

            var movimiento = new MovimientoStock
            {
                Tipo = TiposMovimientoStock.Egreso,
                Fecha = ahora,
                Cantidad = fila.Cantidad,
                IdRepuesto = fila.IdRepuesto,
                IdUsuario = usuarioId,
                Motivo = "Consumo de la orden de trabajo N° " + orden.IdOrden,
                Ubicacion = repuesto.Ubicacion,
                StockAnterior = repuesto.Cantidad,
                StockPosterior = posterior,
            };
            contexto.MovimientoStocks.Add(movimiento);
            fila.IdMovimientoNavigation = movimiento;
            Registrar(orden, fila.IdRepuesto, AccionesRepuestoOrden.Descontar, fila.Cantidad, fila.Cantidad, fila.Estado,
                EstadosRepuestoOrden.Descontado, usuarioId, ahora).IdMovimientoNavigation = movimiento;
            fila.Estado = EstadosRepuestoOrden.Descontado;
        }
    }

    /// <summary>Unidades reservadas y entregadas de cada repuesto, sin contar las de la orden indicada (si se informa).</summary>
    public async Task<Dictionary<long, StockComprometido>> ObtenerComprometidoAsync(
        IReadOnlyCollection<long> repuestosIds, long? exceptoOrdenId, CancellationToken cancelacion)
    {
        var filas = await contexto.OtRepuestos.AsNoTracking()
            .Where(fila => repuestosIds.Contains(fila.IdRepuesto) && EstadosRepuestoOrden.Comprometidos.Contains(fila.Estado)
                && (exceptoOrdenId == null || fila.IdOrden != exceptoOrdenId))
            .GroupBy(fila => new { fila.IdRepuesto, fila.Estado })
            .Select(grupo => new { grupo.Key.IdRepuesto, grupo.Key.Estado, Cantidad = grupo.Sum(fila => fila.Cantidad) })
            .ToListAsync(cancelacion);

        return filas.GroupBy(fila => fila.IdRepuesto).ToDictionary(
            grupo => grupo.Key,
            grupo => new StockComprometido(
                grupo.Where(fila => fila.Estado == EstadosRepuestoOrden.Reservado).Sum(fila => fila.Cantidad),
                grupo.Where(fila => fila.Estado == EstadosRepuestoOrden.Entregado).Sum(fila => fila.Cantidad)));
    }

    private async Task<Dictionary<long, Repuesto>> BloquearAsync(List<long> ids, CancellationToken cancelacion)
    {
        var arreglo = ids.ToArray();
        var repuestos = await contexto.Repuestos
            .FromSqlInterpolated($"SELECT * FROM operativo.repuesto WHERE id_repuesto = ANY({arreglo}) ORDER BY id_repuesto FOR UPDATE")
            .AsNoTracking()
            .ToListAsync(cancelacion);
        return repuestos.ToDictionary(repuesto => repuesto.IdRepuesto);
    }

    private static void ValidarPedidos(IReadOnlyList<RepuestoPedido> pedidos)
    {
        if (pedidos.Any(pedido => pedido.Cantidad < 1 || pedido.Cantidad > CantidadMaxima))
        {
            throw new ValidacionDeNegocioException($"La cantidad de cada repuesto debe estar entre 1 y {CantidadMaxima}.");
        }

        if (pedidos.Select(pedido => pedido.RepuestoId).Distinct().Count() != pedidos.Count)
        {
            throw new ValidacionDeNegocioException("Un repuesto no puede repetirse en la orden: indicá una sola cantidad por repuesto.");
        }
    }

    /// <summary>
    /// Comprueba que haya unidades libres: del stock físico se restan las reservadas y las entregadas a otras órdenes.
    /// El mensaje dice cuál es el problema: no hay stock, o las unidades están reservadas o entregadas.
    /// </summary>
    private static void ExigirDisponible(Repuesto repuesto, int pedido, StockComprometido deOtras)
    {
        var disponible = repuesto.Cantidad - deOtras.Total;
        if (pedido <= disponible)
        {
            return;
        }

        var nombre = Etiqueta(repuesto);
        if (repuesto.Cantidad < pedido)
        {
            var mensaje = repuesto.Cantidad <= 0
                ? $"No hay stock de {nombre}."
                : $"No hay stock suficiente de {nombre}: hay {repuesto.Cantidad} y se necesitan {pedido}.";
            throw new ConflictoDeDatosException(StockInsuficiente, mensaje);
        }

        var partes = new List<string>();
        if (deOtras.Reservado > 0)
        {
            partes.Add($"{deOtras.Reservado} reservadas por otras órdenes");
        }

        if (deOtras.Entregado > 0)
        {
            partes.Add($"{deOtras.Entregado} entregadas a otras órdenes");
        }

        var codigo = deOtras.Reservado >= deOtras.Entregado ? StockReservado : StockEntregado;
        throw new ConflictoDeDatosException(codigo,
            $"No hay unidades disponibles de {nombre}: hay {repuesto.Cantidad} en stock, {string.Join(" y ", partes)}; quedan {Math.Max(0, disponible)} y se necesitan {pedido}.");
    }

    private static string Etiqueta(Repuesto repuesto) => repuesto.Codigo + " - " + repuesto.Nombre;

    private HistorialRepuestoOt Registrar(OrdenTrabajo orden, long repuestoId, string accion, int? cantidadAnterior,
        int cantidadNueva, string? estadoAnterior, string estadoNuevo, long? usuarioId, DateTime fecha)
    {
        var registro = new HistorialRepuestoOt
        {
            IdRepuesto = repuestoId,
            Accion = accion,
            CantidadAnterior = cantidadAnterior,
            CantidadNueva = cantidadNueva,
            EstadoAnterior = estadoAnterior,
            EstadoNuevo = estadoNuevo,
            IdUsuario = usuarioId,
            FechaHora = fecha,
        };
        orden.HistorialRepuestoOts.Add(registro);
        return registro;
    }
}
