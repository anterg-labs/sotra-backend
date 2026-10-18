using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SOTRA_BACKEND.Operaciones;

namespace Api.Infraestructura;

/// <summary>
/// Convierte las excepciones no controladas en respuestas ProblemDetails con un código estable.
/// Nunca devuelve el texto técnico de una excepción inesperada.
/// </summary>
public sealed class ManejadorGlobalDeErrores(
    ILogger<ManejadorGlobalDeErrores> registro,
    IProblemDetailsService servicioProblemas) : IExceptionHandler
{
    private const string MensajeErrorInterno = "Ocurrió un error inesperado. Intentá nuevamente más tarde.";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext contexto, Exception excepcion, CancellationToken cancelacion)
    {
        if (excepcion is OperationCanceledException && contexto.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        var (estado, codigo, mensaje) = Clasificar(excepcion);

        if (estado >= StatusCodes.Status500InternalServerError)
        {
            registro.LogError(excepcion, "Error no controlado en {Metodo} {Ruta}",
                contexto.Request.Method, contexto.Request.Path.Value);
        }
        else
        {
            registro.LogWarning("Solicitud rechazada con {Estado} ({Codigo}) en {Metodo} {Ruta}",
                estado, codigo, contexto.Request.Method, contexto.Request.Path.Value);
        }

        contexto.Response.StatusCode = estado;
        return await servicioProblemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto,
            ProblemDetails =
            {
                Status = estado,
                Extensions = { ["codigo"] = codigo, ["mensaje"] = mensaje },
            },
        });
    }

    private static (int Estado, string Codigo, string Mensaje) Clasificar(Exception excepcion)
    {
        return excepcion switch
        {
            ConflictoDeDatosException conflicto =>
                (StatusCodes.Status409Conflict, conflicto.Codigo, conflicto.Message),
            ArgumentException argumento =>
                (StatusCodes.Status400BadRequest, "SOLICITUD_INVALIDA", argumento.Message),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                (StatusCodes.Status409Conflict, "DATO_DUPLICADO", "Ya existe un registro con esos datos."),
            BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "SOLICITUD_INVALIDA", "La solicitud no es válida."),
            _ => (StatusCodes.Status500InternalServerError, "ERROR_INTERNO", MensajeErrorInterno),
        };
    }
}
