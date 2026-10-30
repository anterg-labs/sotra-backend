namespace Api.Infraestructura;

/// <summary>
/// La fecha de «hoy» según el huso horario de la empresa, el mismo que usa la interfaz. Con UTC la fecha cambia a las 21:00
/// de Uruguay, y una validación «no futura» dejaba pasar el día siguiente.
/// </summary>
public static class FechaDeLaEmpresa
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Montevideo");

    public static DateOnly Hoy(TimeProvider? reloj = null) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime((reloj ?? TimeProvider.System).GetUtcNow(), Zona).DateTime);
}
