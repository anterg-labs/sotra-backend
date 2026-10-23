using Api.Seguridad;
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

        var codigoHttp = resultado.Exception switch
        {
            OperationCanceledException when contexto.HttpContext.RequestAborted.IsCancellationRequested =>
                CodigoClienteCerroLaSolicitud,
            not null => StatusCodes.Status500InternalServerError,
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
            contexto.RouteData.Values.TryGetValue("id", out var id) ? id : null,
            codigoHttp,
            usuarioActual.Correo ?? "sin autenticar");
    }

    private static string ObtenerAccion(ActionExecutingContext contexto)
    {
        return contexto.HttpContext.Request.Method + " " + contexto.RouteData.Values["action"];
    }
}
