using System.Net;

namespace Api.Servicios;

/// <summary>Cómo se redactó una sugerencia; se devuelve al frontend y queda en el historial.</summary>
public static class ModosDeSugerencia
{
    /// <summary>Resumen del proveedor de IA que pasó el control de números.</summary>
    public const string Ia = "ia";

    /// <summary>El proveedor no respondió o no está configurado: texto del manual sin resumir, con aviso.</summary>
    public const string SinIa = "sin_ia";

    /// <summary>El proveedor respondió, pero el control de números descartó el resumen: texto del manual, sin aviso.</summary>
    public const string Descartada = "descartada";

    /// <summary>El proveedor leyó la sección y dice que no trata la tarea: no hay sugerencia para mostrar.</summary>
    public const string SinInformacion = "sin_informacion";
}

/// <summary>
/// Texto y modo de la sugerencia. <c>Titulo</c> es el título en español que redactó la IA; vacío sin IA.
/// <c>Advertencias</c> son las del manual traducidas por la IA, o <c>null</c> si se muestran las originales.
/// </summary>
public record SugerenciaRedactada(string Texto, string Modo, string Titulo = "", List<string>? Advertencias = null);

public class RedactorDeSugerencias
{
    private readonly IGeneradorDeTexto _generador;
    private readonly ILogger<RedactorDeSugerencias> _logger;

    public RedactorDeSugerencias(IGeneradorDeTexto generador, ILogger<RedactorDeSugerencias> logger)
    {
        _generador = generador;
        _logger = logger;
    }

    /// <summary>
    /// Pide el resumen al proveedor. Las <paramref name="advertencias"/> de un manual en otro idioma se piden traducidas
    /// en la misma llamada; si el modelo no devuelve una por cada una, se muestran las originales.
    /// </summary>
    public async Task<SugerenciaRedactada> ResumirAsync(string consulta, SeccionDocumento seccion,
        string textoParaResumir, CancellationToken cancellationToken, List<string>? advertencias = null)
    {
        var resumenSinIa = TextoDelManual.CrearResumenExtractivo(textoParaResumir);
        var advertenciasATraducir = advertencias is { Count: > 0 }
            && TraductorDeConsultas.EstaEnIngles(string.Join(" ", advertencias)) ? advertencias : null;
        var mensaje = RedaccionDeSugerencia.CrearMensajeParaElModelo(consulta, seccion, textoParaResumir,
            advertenciasATraducir);

        RespuestaDelModelo respuesta;
        try
        {
            var generado = await _generador.GenerarAsync(RedaccionDeSugerencia.Instrucciones, mensaje, cancellationToken);
            if (RedaccionDeSugerencia.EsRespuestaSinInformacion(generado))
            {
                return new SugerenciaRedactada("", ModosDeSugerencia.SinInformacion);
            }

            respuesta = RedaccionDeSugerencia.SepararTitulo(generado);
        }
        catch (GeneradorDeTextoNoDisponibleException excepcion)
        {
            RegistrarFallo(excepcion);
            return new SugerenciaRedactada(resumenSinIa, ModosDeSugerencia.SinIa);
        }
        catch (OperationCanceledException excepcion) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(excepcion, "El proveedor de IA no respondió a tiempo; se usa el texto del manual sin resumir");
            return new SugerenciaRedactada(resumenSinIa, ModosDeSugerencia.SinIa);
        }

        var traducidas = advertenciasATraducir is not null && respuesta.Advertencias?.Count == advertenciasATraducir.Count
            ? respuesta.Advertencias
            : null;
        var otrosTextos = respuesta.Titulo + " " + string.Join(" ", traducidas ?? []);
        var motivo = RedaccionDeSugerencia.MotivoDeDescarte(respuesta.Resumen, consulta, seccion, otrosTextos);
        if (motivo is not null)
        {
            // Sin este registro no hay forma de saber después por qué la persona vio el texto del manual.
            _logger.LogWarning("Se descartó el resumen de la IA ({Motivo}) y se usa el texto del manual. Sección: {Seccion}, página {Pagina}. Comienzo de la respuesta: {Comienzo}",
                motivo, seccion.Titulo, seccion.Pagina, Comienzo(respuesta.Titulo + " | " + respuesta.Resumen));
            return new SugerenciaRedactada(resumenSinIa, ModosDeSugerencia.Descartada);
        }

        return new SugerenciaRedactada(respuesta.Resumen, ModosDeSugerencia.Ia, respuesta.Titulo, traducidas);
    }

    private static string Comienzo(string texto)
    {
        const int Largo = 200;
        var limpio = texto.Replace('\n', ' ').Trim();
        return limpio.Length <= Largo ? limpio : limpio.Substring(0, Largo) + "…";
    }

    // La clave inválida, el cupo agotado o la falta de configuración no se arreglan solos: van como error para que
    // alguien del equipo se entere. Una caída o una demora del proveedor es pasajera.
    private void RegistrarFallo(GeneradorDeTextoNoDisponibleException excepcion)
    {
        var requiereAtencion = excepcion.CodigoHttp is null && excepcion.InnerException is null
            || excepcion.CodigoHttp is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                or HttpStatusCode.PaymentRequired or HttpStatusCode.NotFound or HttpStatusCode.BadRequest;
        if (requiereAtencion)
        {
            _logger.LogError(excepcion, "El proveedor de IA rechazó el pedido o no está configurado; se usa el texto del manual sin resumir");
            return;
        }

        _logger.LogWarning(excepcion, "El proveedor de IA no está disponible; se usa el texto del manual sin resumir");
    }
}
