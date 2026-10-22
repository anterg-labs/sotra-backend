using Api.DTOs.Repuestos;
using SOTRA_BACKEND.Modelos;

namespace Api.Mapeadores;

public static class RepuestoMapeador
{
    public static Repuesto AEntidad(RepuestoCrearDto solicitud)
    {
        return new Repuesto
        {
            Codigo = solicitud.Codigo.Trim(),
            Nombre = solicitud.Nombre.Trim(),
            Descripcion = solicitud.Descripcion,
            Marca = solicitud.Marca,
            Ubicacion = solicitud.Ubicacion,
            Cantidad = solicitud.Cantidad,
            CantMin = solicitud.CantidadMinima,
            Estado = true
        };
    }

    public static void AplicarCambios(Repuesto repuesto, RepuestoActualizarDto solicitud)
    {
        repuesto.Codigo = solicitud.Codigo.Trim();
        repuesto.Nombre = solicitud.Nombre.Trim();
        repuesto.Descripcion = solicitud.Descripcion;
        repuesto.Marca = solicitud.Marca;
        repuesto.Ubicacion = solicitud.Ubicacion;
        repuesto.Cantidad = solicitud.Cantidad;
        repuesto.CantMin = solicitud.CantidadMinima;
    }

    public const string StockNormal = "normal";
    public const string StockBajo = "bajo";
    public const string SinStock = "sin_stock";

    // Sin unidades: sin_stock. En el mínimo o por debajo: bajo (el mismo criterio que los indicadores).
    public static string CalcularEstadoStock(int cantidad, int cantidadMinima)
    {
        if (cantidad <= 0)
        {
            return SinStock;
        }

        return cantidad <= cantidadMinima ? StockBajo : StockNormal;
    }

    public static RepuestoDto ADto(Repuesto repuesto)
    {
        return new RepuestoDto
        {
            Id = repuesto.IdRepuesto,
            Codigo = repuesto.Codigo,
            Nombre = repuesto.Nombre,
            Descripcion = repuesto.Descripcion ?? "",
            Marca = repuesto.Marca ?? "",
            Ubicacion = repuesto.Ubicacion ?? "",
            Cantidad = repuesto.Cantidad,
            CantidadMinima = repuesto.CantMin ?? 0,
            Estado = repuesto.Estado ? "activo" : "inactivo",
            EstadoStock = CalcularEstadoStock(repuesto.Cantidad, repuesto.CantMin ?? 0)
        };
    }
}
