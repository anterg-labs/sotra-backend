using Api.DTOs.MovimientosStock;
using SOTRA_BACKEND.Modelos;

namespace Api.Mapeadores;

public static class MovimientoStockMapeador
{
    public static MovimientoStockDto ADto(MovimientoStock movimiento)
    {
        string? nombreUsuario = null;
        if (movimiento.IdUsuarioNavigation is not null)
        {
            nombreUsuario = movimiento.IdUsuarioNavigation.Nombre + " " + movimiento.IdUsuarioNavigation.Apellido;
        }

        var ubicacion = "";
        if (movimiento.Ubicacion is not null)
        {
            ubicacion = movimiento.Ubicacion;
        }

        var motivo = "";
        if (movimiento.Motivo is not null)
        {
            motivo = movimiento.Motivo;
        }

        var observaciones = "";
        if (movimiento.Observaciones is not null)
        {
            observaciones = movimiento.Observaciones;
        }

        return new MovimientoStockDto
        {
            Id = movimiento.IdMovimiento,
            RepuestoId = movimiento.IdRepuesto,
            CodigoRepuesto = movimiento.IdRepuestoNavigation.Codigo,
            NombreRepuesto = movimiento.IdRepuestoNavigation.Nombre,
            Tipo = movimiento.Tipo,
            Cantidad = movimiento.Cantidad,
            Fecha = movimiento.Fecha,
            Ubicacion = ubicacion,
            UsuarioId = movimiento.IdUsuario,
            NombreUsuario = nombreUsuario,
            Motivo = motivo,
            Observaciones = observaciones,
            StockAnterior = movimiento.StockAnterior,
            StockPosterior = movimiento.StockPosterior
        };
    }
}
