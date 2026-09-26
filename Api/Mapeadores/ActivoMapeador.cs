using System.Linq.Expressions;
using Api.DTOs.Activos;
using SOTRA_BACKEND.Modelos;

namespace Api.Mapeadores;

public static class ActivoMapeador
{
    public static Expression<Func<Activo, ActivoDto>> Proyeccion => activo =>
        new ActivoDto
        {
            Id = activo.IdActivo,
            ClienteId = activo.IdCliente,
            NumSerie = activo.NumSerie,
            Descripcion = activo.Descripcion,
            ModeloMaquinaId = activo.IdModeloMaquina,
            AnioFabricacion = activo.AnioFabricacion,
            Nombre = activo.Nombre,
            Ubicacion = activo.Ubicacion,
            Estado = activo.Estado ? "activo" : "inactivo"
        };

    public static Activo AEntidad(ActivoCrearDto activo)
    {
        return new Activo
        {
            IdCliente = activo.ClienteId,
            NumSerie = activo.NumSerie,
            Descripcion = activo.Descripcion,
            IdModeloMaquina = activo.ModeloMaquinaId,
            AnioFabricacion = activo.AnioFabricacion,
            Nombre = activo.Nombre,
            Ubicacion = activo.Ubicacion,
            Estado = true
        };
    }

    public static void AplicarCambios(Activo destino, ActivoActualizarDto cambios)
    {
        if (cambios.ClienteId.HasValue) destino.IdCliente = cambios.ClienteId.Value;
        if (!string.IsNullOrWhiteSpace(cambios.NumSerie)) destino.NumSerie = cambios.NumSerie;
        if (!string.IsNullOrWhiteSpace(cambios.Descripcion)) destino.Descripcion = cambios.Descripcion;
        if (cambios.ModeloMaquinaId.HasValue) destino.IdModeloMaquina = cambios.ModeloMaquinaId.Value;
        if (cambios.AnioFabricacion.HasValue) destino.AnioFabricacion = cambios.AnioFabricacion.Value;
        if (!string.IsNullOrWhiteSpace(cambios.Nombre)) destino.Nombre = cambios.Nombre;
        if (!string.IsNullOrWhiteSpace(cambios.Ubicacion)) destino.Ubicacion = cambios.Ubicacion;
    }

    public static ActivoDto ADto(Activo activo)
    {
        return new ActivoDto
        {
            Id = activo.IdActivo,
            ClienteId = activo.IdCliente,
            NumSerie = activo.NumSerie,
            Descripcion = activo.Descripcion,
            ModeloMaquinaId = activo.IdModeloMaquina,
            AnioFabricacion = activo.AnioFabricacion,
            Nombre = activo.Nombre,
            Ubicacion = activo.Ubicacion,
            Estado = activo.Estado ? "activo" : "inactivo"
        };
    }
}
