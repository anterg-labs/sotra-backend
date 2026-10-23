using Microsoft.AspNetCore.Mvc;

namespace Api.Infraestructura;

public static class RespuestasDeError
{
    /// <summary>Error de una columna de orden que no está en la lista cerrada del listado.</summary>
    public static ObjectResult OrdenInvalido(this ControllerBase controlador, IEnumerable<string> columnasPermitidas) =>
        controlador.RespuestaDeError(400, "ORDEN_INVALIDO",
            "No se puede ordenar por esa columna. Columnas permitidas: " + string.Join(", ", columnasPermitidas) + ".");

    /// <summary>
    /// Respuesta de error con el mismo formato que el manejo global: <c>ProblemDetails</c> con <c>codigo</c> estable,
    /// <c>mensaje</c> para mostrar y, si existe, <c>idCorrelacion</c>.
    /// </summary>
    public static ObjectResult RespuestaDeError(this ControllerBase controlador, int estado, string codigo, string mensaje)
    {
        var detalles = controlador.ProblemDetailsFactory.CreateProblemDetails(controlador.HttpContext, statusCode: estado);
        detalles.Extensions["codigo"] = codigo;
        detalles.Extensions["mensaje"] = mensaje;
        if (controlador.HttpContext.Items.TryGetValue(CorrelacionMiddleware.Cabecera, out var correlacion))
        {
            detalles.Extensions["idCorrelacion"] = correlacion;
        }

        return new ObjectResult(detalles)
        {
            StatusCode = estado,
            ContentTypes = { "application/problem+json" },
        };
    }
}
