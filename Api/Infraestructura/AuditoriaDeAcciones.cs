using Api.Seguridad;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Api.Infraestructura;

/// <summary>
/// Deja un registro de auditoría de cada acción que cambia datos (alta, modificación, baja, movimientos y cambios de
/// estado): qué se hizo, sobre qué recurso, quién lo pidió y con qué resultado. Nunca guarda el cuerpo de la
/// solicitud, para no copiar datos personales ni contraseñas a los logs.
/// </summary>
public sealed class AuditoriaDeAcciones(ILogger<AuditoriaDeAcciones> registro, IUsuarioActual usuarioActual) : IAsyncActionFilter
{
    private const int CodigoClienteCerroLaSolicitud = 499;

    public async Task OnActionExecutionAsync(ActionExecutingContext contexto, ActionExecutionDelegate siguiente)
    {
        var resultado = await siguiente();

        var metodo = contexto.HttpContext.Request.Method;
        if (HttpMethods.IsGet(metodo) || HttpMethods.IsHead(metodo) || HttpMethods.IsOptions(metodo))
        {
            return;
        }

        // El manejador global traduce las excepciones de negocio después de este filtro: se usa la misma tabla para
        // no auditar como 500 un 400, 404, 409 o 412 que la persona recibirá.
        var codigoHttp = resultado.Exception switch
        {
            OperationCanceledException when contexto.HttpContext.RequestAborted.IsCancellationRequested =>
                CodigoClienteCerroLaSolicitud,
            { } excepcion => TraduccionDeErroresDeNegocio.Traducir(excepcion)?.Estado ?? StatusCodes.Status500InternalServerError,
            _ => (resultado.Result as IStatusCodeActionResult)?.StatusCode ?? contexto.HttpContext.Response.StatusCode,
        };

        MetricasDeNegocio.Acciones.Add(1,
            new KeyValuePair<string, object?>("recurso", contexto.RouteData.Values["controller"]),
            new KeyValuePair<string, object?>("accion", ObtenerAccion(contexto)),
            new KeyValuePair<string, object?>("resultado", MetricasDeNegocio.ClaseDeResultado(codigoHttp)));

        registro.LogInformation(
            "Auditoria {Accion} {Controlador} recurso {RecursoId} resultado {CodigoHttp} usuario {Usuario}",
            ObtenerAccion(contexto),
            contexto.RouteData.Values["controller"],
            ObtenerRecurso(contexto, resultado.Result),
            codigoHttp,
            usuarioActual.Correo ?? "sin autenticar");
    }

    // El recurso es el `id` de la ruta, el primer parámetro `...Id` (por ejemplo `ordenTrabajoId` en los subrecursos)
    // o, en una alta, el identificador del recurso creado.
    private static object? ObtenerRecurso(ActionExecutingContext contexto, IActionResult? resultado)
    {
        var valores = contexto.RouteData.Values;
        if (valores.TryGetValue("id", out var id))
        {
            return id;
        }

        var deLaRuta = valores.FirstOrDefault(par => par.Key.EndsWith("Id", StringComparison.Ordinal) && par.Value is not null);
        if (deLaRuta.Value is not null)
        {
            return deLaRuta.Value;
        }

        return resultado is ObjectResult { Value: { } creado }
            ? creado.GetType().GetProperty("Id")?.GetValue(creado)
            : null;
    }

    private static string ObtenerAccion(ActionExecutingContext contexto)
    {
        return contexto.HttpContext.Request.Method + " " + contexto.RouteData.Values["action"];
    }
}
