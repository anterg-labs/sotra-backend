using Api.DTOs.Activos;
using SOTRA_BACKEND.Modelos;

namespace Api.Mapeadores;

public static class ActivoMapeador
{
    public static Activo AEntidad(ActivoCrearDto activo)
    {
        return new Activo
        {
            IdCliente = activo.ClienteId,
            NumSerie = activo.NumSerie,
            Descripcion = TextoOpcional.Normalizar(activo.Descripcion),
            IdModeloMaquina = activo.ModeloMaquinaId,
            AnioFabricacion = activo.AnioFabricacion,
            Nombre = TextoOpcional.Normalizar(activo.Nombre),
            Ubicacion = TextoOpcional.Normalizar(activo.Ubicacion),
            UnidadUso = activo.UnidadUso ?? "horas",
            Estado = true
        };
    }

    public static void AplicarCambios(Activo destino, ActivoActualizarDto cambios)
    {
        if (cambios.ClienteId is null || cambios.ModeloMaquinaId is null || string.IsNullOrWhiteSpace(cambios.NumSerie))
            throw new ArgumentException("El cliente, el modelo y el número de serie son obligatorios.");
        if (cambios.Estado is not ("activo" or "inactivo"))
            throw new ArgumentException("El estado debe ser 'activo' o 'inactivo'.");

        destino.IdCliente = cambios.ClienteId.Value;
        destino.NumSerie = cambios.NumSerie;
        destino.Descripcion = TextoOpcional.Normalizar(cambios.Descripcion);
        destino.IdModeloMaquina = cambios.ModeloMaquinaId.Value;
        destino.AnioFabricacion = cambios.AnioFabricacion;
        destino.Nombre = TextoOpcional.Normalizar(cambios.Nombre);
        destino.Ubicacion = TextoOpcional.Normalizar(cambios.Ubicacion);
        if (cambios.UnidadUso is not null)
        {
            destino.UnidadUso = cambios.UnidadUso;
        }

        if (cambios.Estado == "activo")
        {
            destino.Estado = true;
        }
        else if (cambios.Estado == "inactivo")
        {
            destino.Estado = false;
        }
        else if (cambios.Estado != null)
        {
            throw new ArgumentException("El estado debe ser 'activo' o 'inactivo'.");
        }
    }

    public static ActivoDto ADto(Activo activo)
    {
        return new ActivoDto
        {
            Id = activo.IdActivo,
            ClienteId = activo.IdCliente,
            NombreCliente = activo.IdClienteNavigation?.RazonSocial,
            NumSerie = activo.NumSerie,
            Descripcion = activo.Descripcion,
            ModeloMaquinaId = activo.IdModeloMaquina,
            NombreModeloMaquina = activo.IdModeloMaquinaNavigation?.Nombre,
            TipoMaquinaId = activo.IdModeloMaquinaNavigation?.IdTipoMaquina,
            NombreTipoMaquina = activo.IdModeloMaquinaNavigation?.IdTipoMaquinaNavigation?.Nombre,
            AnioFabricacion = activo.AnioFabricacion,
            Nombre = activo.Nombre,
            Ubicacion = activo.Ubicacion,
            UnidadUso = activo.UnidadUso,
            Estado = activo.Estado ? "activo" : "inactivo"
        };
    }
}
