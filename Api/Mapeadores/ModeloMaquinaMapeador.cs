using Api.DTOs.ModelosMaquina;
using SOTRA_BACKEND.Modelos;

namespace Api.Mapeadores;

public static class ModeloMaquinaMapeador
{
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
        destino.Nombre = cambios.Nombre;
        destino.IdTipoMaquina = cambios.TipoMaquinaId;
        destino.Descripcion = cambios.Descripcion;
        if (string.IsNullOrWhiteSpace(destino.Descripcion))
        {
            destino.Descripcion = null;
        }

        if (cambios.Estado == "activo")
        {
            destino.Activo = true;
        }
        else if (cambios.Estado == "inactivo")
        {
            destino.Activo = false;
        }
        else if (cambios.Estado != null)
        {
            throw new ArgumentException("El estado debe ser 'activo' o 'inactivo'.");
        }
    }

    public static ModeloMaquinaDto ADto(ModeloMaquina modelo)
    {
        return new ModeloMaquinaDto
        {
            Id = modelo.IdModeloMaquina,
            Nombre = modelo.Nombre,
            TipoMaquinaId = modelo.IdTipoMaquina,
            NombreTipoMaquina = modelo.IdTipoMaquinaNavigation?.Nombre,
            Descripcion = modelo.Descripcion,
            Estado = modelo.Activo ? "activo" : "inactivo"
        };
    }
}
