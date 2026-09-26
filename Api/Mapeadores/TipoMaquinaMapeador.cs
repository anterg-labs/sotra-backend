using System.Linq.Expressions;
using Api.DTOs.TiposMaquina;
using SOTRA_BACKEND.Modelos;

namespace Api.Mapeadores;

public static class TipoMaquinaMapeador
{
    public static Expression<Func<TipoMaquina, TipoMaquinaDto>> Proyeccion => tipo =>
        new TipoMaquinaDto
        {
            Id = tipo.IdTipoMaquina,
            Nombre = tipo.Nombre,
            Estado = tipo.Estado ? "activo" : "inactivo"
        };

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
        if (!string.IsNullOrWhiteSpace(cambios.Nombre))
            destino.Nombre = cambios.Nombre;
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
