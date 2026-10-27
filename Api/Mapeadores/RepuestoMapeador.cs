using Api.DTOs.Repuestos;
using SOTRA_BACKEND.Modelos;
using SOTRA_BACKEND.Operaciones;

namespace Api.Mapeadores;

public static class RepuestoMapeador
{
    public static Repuesto AEntidad(RepuestoCrearDto solicitud)
    {
        return new Repuesto
        {
            Codigo = solicitud.Codigo.Trim(),
            Nombre = solicitud.Nombre.Trim(),
            Descripcion = TextoOpcional.Normalizar(solicitud.Descripcion),
            Marca = TextoOpcional.Normalizar(solicitud.Marca),
            Ubicacion = TextoOpcional.Normalizar(solicitud.Ubicacion),
            Cantidad = solicitud.Cantidad,
            CantMin = solicitud.CantidadMinima,
            Estado = true
        };
    }

    public static void AplicarCambios(Repuesto repuesto, RepuestoActualizarDto solicitud)
    {
        repuesto.Codigo = solicitud.Codigo.Trim();
        repuesto.Nombre = solicitud.Nombre.Trim();
        repuesto.Descripcion = TextoOpcional.Normalizar(solicitud.Descripcion);
        repuesto.Marca = TextoOpcional.Normalizar(solicitud.Marca);
        repuesto.Ubicacion = TextoOpcional.Normalizar(solicitud.Ubicacion);
        repuesto.CantMin = solicitud.CantidadMinima;
    }

    public static RepuestoDto ADto(Repuesto repuesto, StockComprometido? comprometido = null)
    {
        var reservado = comprometido?.Reservado ?? 0;
        var entregado = comprometido?.Entregado ?? 0;
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
            CantidadReservada = reservado,
            CantidadEntregada = entregado,
            CantidadDisponible = Math.Max(0, repuesto.Cantidad - reservado - entregado),
            Estado = repuesto.Estado ? "activo" : "inactivo",
            EstadoStock = EstadosStock.Calcular(repuesto.Cantidad, repuesto.CantMin)
        };
    }
}
