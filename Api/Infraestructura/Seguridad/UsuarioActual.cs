using Microsoft.Extensions.Options;

namespace Api.Infraestructura.Seguridad;

/// <summary>La persona que hace la solicitud. Con la seguridad apagada no hay nadie autenticado.</summary>
public interface IUsuarioActual
{
    bool EstaAutenticado { get; }

    string? Correo { get; }
}

public sealed class UsuarioActual(IHttpContextAccessor accesor, IOptions<OpcionesDeSeguridad> opciones) : IUsuarioActual
{
    public bool EstaAutenticado => accesor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public string? Correo => EstaAutenticado
        ? accesor.HttpContext!.User.FindFirst(opciones.Value.ClaimCorreo)?.Value
        : null;
}
