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
        repuesto.CantMin = solicitud.CantidadMinima;
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
            EstadoStock = EstadosStock.Calcular(repuesto.Cantidad, repuesto.CantMin)
        };
    }
}
