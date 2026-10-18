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
            AnioFabricacion = activo.AnioFabricacion,
            Nombre = activo.Nombre,
            Ubicacion = activo.Ubicacion,
            Estado = activo.Estado ? "activo" : "inactivo"
        };
    }
}
