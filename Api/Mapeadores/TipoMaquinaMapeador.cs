using Api.DTOs.TiposMaquina;
using SOTRA_BACKEND.Modelos;

namespace Api.Mapeadores;

public static class TipoMaquinaMapeador
{
    public static TipoMaquina AEntidad(TipoMaquinaCrearDto tipo)
    {
        return new TipoMaquina
        {
            Nombre = tipo.Nombre,
            Estado = true
        };
    }

    public static void AplicarCambios(TipoMaquina destino, TipoMaquinaActualizarDto cambios)
    {
        destino.Nombre = cambios.Nombre;

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

    public static TipoMaquinaDto ADto(TipoMaquina tipo)
    {
        return new TipoMaquinaDto
        {
            Id = tipo.IdTipoMaquina,
            Nombre = tipo.Nombre,
            Estado = tipo.Estado ? "activo" : "inactivo"
        };
    }
}
