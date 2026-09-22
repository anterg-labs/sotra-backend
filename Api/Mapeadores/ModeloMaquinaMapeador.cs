using System.Linq.Expressions;
using Api.DTOs.ModelosMaquina;
using SOTRA_BACKEND.Modelos;

namespace Api.Mapeadores;

public static class ModeloMaquinaMapeador
{
    public static Expression<Func<ModeloMaquina, ModeloMaquinaDto>> Proyeccion => modelo =>
        new ModeloMaquinaDto
        {
            Id = modelo.IdModeloMaquina,
            Nombre = modelo.Nombre,
            TipoMaquinaId = modelo.IdTipoMaquina,
            Descripcion = modelo.Descripcion,
            Estado = modelo.Activo ? "activo" : "inactivo"
        };

    public static ModeloMaquina AEntidad(ModeloMaquinaCrearDto modelo)
    {
        return new ModeloMaquina
        {
            Nombre = modelo.Nombre,
            IdTipoMaquina = modelo.TipoMaquinaId,
            Descripcion = modelo.Descripcion,
            Activo = true
        };
    }

    public static void AplicarCambios(ModeloMaquina destino, ModeloMaquinaActualizarDto cambios)
    {
        if (!string.IsNullOrWhiteSpace(cambios.Nombre)) destino.Nombre = cambios.Nombre;
        if (cambios.TipoMaquinaId.HasValue) destino.IdTipoMaquina = cambios.TipoMaquinaId.Value;
        if (!string.IsNullOrWhiteSpace(cambios.Descripcion)) destino.Descripcion = cambios.Descripcion;
    }

    public static ModeloMaquinaDto ADto(ModeloMaquina modelo)
    {
        return new ModeloMaquinaDto
        {
            Id = modelo.IdModeloMaquina,
            Nombre = modelo.Nombre,
            TipoMaquinaId = modelo.IdTipoMaquina,
            Descripcion = modelo.Descripcion,
            Estado = modelo.Activo ? "activo" : "inactivo"
        };
    }
}
