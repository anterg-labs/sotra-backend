using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Api.Infraestructura.Seguridad;

/// <summary>Exige un permiso de la tabla <c>permiso</c> (por ejemplo <c>usuario.crear</c>). Sin seguridad, no restringe nada.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class PermisoAttribute(string permiso) : AuthorizeAttribute($"{Prefijo}{permiso}")
{
    public const string Prefijo = "permiso:";
}

public sealed record RequisitoDePermiso(string Permiso) : IAuthorizationRequirement;

public sealed class ManejadorDeRequisitoDePermiso(IOptions<OpcionesDeSeguridad> opciones)
    : AuthorizationHandler<RequisitoDePermiso>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext contexto, RequisitoDePermiso requisito)
    {
        var configuracion = opciones.Value;
        var estaAutorizado = contexto.User.Identity?.IsAuthenticated == true
            && TienePermiso(contexto.User, configuracion.ClaimPermisos, requisito.Permiso);
        if (!configuracion.Habilitada || estaAutorizado)
        {
            contexto.Succeed(requisito);
        }

        return Task.CompletedTask;
    }

    private static bool TienePermiso(System.Security.Claims.ClaimsPrincipal usuario, string claim, string permiso)
    {
        return usuario.FindAll(claim)
            .SelectMany(item => item.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(permiso, StringComparer.Ordinal);
    }
}

/// <summary>Arma la política de cada permiso a partir de su nombre, sin declarar una por una.</summary>
public sealed class ProveedorDePoliticasDePermiso(IOptions<AuthorizationOptions> opciones)
    : DefaultAuthorizationPolicyProvider(opciones)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string nombre)
    {
        if (nombre.StartsWith(PermisoAttribute.Prefijo, StringComparison.Ordinal))
        {
            return new AuthorizationPolicyBuilder()
                .AddRequirements(new RequisitoDePermiso(nombre[PermisoAttribute.Prefijo.Length..]))
                .Build();
        }

        return await base.GetPolicyAsync(nombre);
    }
}
