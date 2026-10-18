namespace Api.Infraestructura;

/// <summary>
/// Cabeceras de seguridad para una API que solo devuelve datos y archivos: sin contenido activo,
/// sin incrustación en marcos y sin caché de respuestas con información de negocio.
/// </summary>
public sealed class CabecerasDeSeguridadMiddleware(RequestDelegate siguiente)
{
    public Task InvokeAsync(HttpContext contexto)
    {
        contexto.Response.OnStarting(() =>
        {
            var cabeceras = contexto.Response.Headers;
            cabeceras["X-Content-Type-Options"] = "nosniff";
            cabeceras["X-Frame-Options"] = "DENY";
            cabeceras["Referrer-Policy"] = "no-referrer";
            cabeceras["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            cabeceras["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            cabeceras["Cross-Origin-Resource-Policy"] = "same-site";

            if (contexto.Request.Path.StartsWithSegments("/api") && !cabeceras.ContainsKey("Cache-Control"))
            {
                cabeceras["Cache-Control"] = "no-store";
            }

            return Task.CompletedTask;
        });

        return siguiente(contexto);
    }
}
