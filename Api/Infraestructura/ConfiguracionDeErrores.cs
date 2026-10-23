using Microsoft.AspNetCore.Mvc;

namespace Api.Infraestructura;

public static class ConfiguracionDeErrores
{
    private static readonly Dictionary<int, string> CodigosPorEstado = new()
    {
        [StatusCodes.Status400BadRequest] = "SOLICITUD_INVALIDA",
        [StatusCodes.Status401Unauthorized] = "NO_AUTENTICADO",
        [StatusCodes.Status403Forbidden] = "SIN_PERMISO",
        [StatusCodes.Status404NotFound] = "RECURSO_NO_ENCONTRADO",
        [StatusCodes.Status405MethodNotAllowed] = "METODO_NO_PERMITIDO",
        [StatusCodes.Status409Conflict] = "CONFLICTO",
        [StatusCodes.Status415UnsupportedMediaType] = "TIPO_DE_CONTENIDO_NO_SOPORTADO",
        [StatusCodes.Status429TooManyRequests] = "DEMASIADAS_SOLICITUDES",
        [StatusCodes.Status500InternalServerError] = "ERROR_INTERNO",
    };

    public static IServiceCollection AgregarManejoDeErrores(this IServiceCollection servicios)
    {
        servicios.AddProblemDetails(opciones => opciones.CustomizeProblemDetails = contexto =>
        {
            var detalles = contexto.ProblemDetails;
            if (!detalles.Extensions.ContainsKey("codigo")
                && detalles.Status is { } estado
                && CodigosPorEstado.TryGetValue(estado, out var codigo))
            {
                detalles.Extensions["codigo"] = codigo;
            }

            if (contexto.HttpContext.Items.TryGetValue(CorrelacionMiddleware.Cabecera, out var correlacion))
            {
                detalles.Extensions["idCorrelacion"] = correlacion;
            }
        });

        servicios.AddExceptionHandler<ManejadorGlobalDeErrores>();
        servicios.PostConfigure<ApiBehaviorOptions>(opciones =>
            opciones.InvalidModelStateResponseFactory = CrearRespuestaDeValidacion);
        return servicios;
    }

    private static BadRequestObjectResult CrearRespuestaDeValidacion(ActionContext contexto)
    {
        var detalles = new ValidationProblemDetails(contexto.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Hay datos inválidos en la solicitud.",
        };
        detalles.Extensions["codigo"] = "VALIDACION_FALLIDA";
        detalles.Extensions["mensaje"] = PrimerMensajeDeCampo(detalles.Errors)
            ?? "Revisá los datos ingresados.";

        return new BadRequestObjectResult(detalles) { ContentTypes = { "application/problem+json" } };
    }

    /// <summary>Usa el texto del primer campo validado por la API; ignora los errores de lectura del JSON.</summary>
    private static string? PrimerMensajeDeCampo(IDictionary<string, string[]> errores)
    {
        return errores
            .Where(error => error.Key is not ("" or "$" or "solicitud") && !error.Key.StartsWith("$."))
            .SelectMany(error => error.Value)
            .FirstOrDefault();
    }
}
