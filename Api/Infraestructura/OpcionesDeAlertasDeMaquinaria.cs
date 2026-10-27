using System.ComponentModel.DataAnnotations;

namespace Api.Infraestructura;

/// <summary>Umbrales de las alertas de maquinaria, ajustables por configuración sin tocar el código.</summary>
public sealed class OpcionesDeAlertasDeMaquinaria
{
    public const string Seccion = "AlertasMaquinaria";

    /// <summary>Horas que una orden puede quedar «pendiente» sin técnico antes de avisar que falta asignarla.</summary>
    [Range(0, 100_000)]
    public int SinAsignarHoras { get; init; } = 24;

    /// <summary>Horas que una orden «pendiente» con técnico puede quedar sin iniciarse (desde la última asignación).</summary>
    [Range(0, 100_000)]
    public int AsignadaSinIniciarHoras { get; init; } = 48;

    /// <summary>Horas máximas en «pausado».</summary>
    [Range(0, 100_000)]
    public int PausadaHoras { get; init; } = 72;

    /// <summary>Horas desde la creación de una orden abierta de prioridad alta antes de considerar la máquina detenida demasiado tiempo.</summary>
    [Range(0, 100_000)]
    public int MaquinaDetenidaAltaHoras { get; init; } = 48;

    [Range(0, 100_000)]
    public int MaquinaDetenidaMediaHoras { get; init; } = 120;

    [Range(0, 100_000)]
    public int MaquinaDetenidaBajaHoras { get; init; } = 240;

    /// <summary>Días para considerar una correctiva como reincidencia de la anterior del mismo activo.</summary>
    [Range(1, 3650)]
    public int ReincidenciaDias { get; init; } = 90;
}
