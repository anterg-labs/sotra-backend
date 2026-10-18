using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Api.Infraestructura.Seguridad;

public static class ConfiguracionDeSeguridad
{
    public static IServiceCollection AgregarSeguridad(
        this IServiceCollection servicios, IConfiguration configuracion)
    {
        var opciones = LeerOpciones(configuracion);
        if (!opciones.EsValida(out var error))
        {
            throw new InvalidOperationException(error);
        }

        servicios.AddSingleton(Microsoft.Extensions.Options.Options.Create(opciones));
        servicios.AddSingleton<IAuthorizationHandler, ManejadorDeRequisitoDePermiso>();
        servicios.AddSingleton<IAuthorizationPolicyProvider, ProveedorDePoliticasDePermiso>();
        servicios.AddHttpContextAccessor();
        servicios.AddScoped<IUsuarioActual, UsuarioActual>();

        var autorizacion = servicios.AddAuthorization(configuracionAutorizacion =>
        {
            if (opciones.Habilitada)
            {
                configuracionAutorizacion.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            }
        });

        if (opciones.Habilitada)
        {
            servicios.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(jwt =>
                {
                    jwt.Authority = opciones.Emisor;
                    jwt.Audience = opciones.Audiencia;
                    jwt.RequireHttpsMetadata = !new Uri(opciones.Emisor!).IsLoopback;
                    jwt.TokenValidationParameters.ValidateIssuer = true;
                    jwt.TokenValidationParameters.ValidateAudience = true;
                    jwt.TokenValidationParameters.ValidateLifetime = true;
                    jwt.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
                });
        }

        return autorizacion;
    }

    public static OpcionesDeSeguridad LeerOpciones(IConfiguration configuracion)
    {
        var base_ = configuracion.GetSection(OpcionesDeSeguridad.Seccion).Get<OpcionesDeSeguridad>()
                    ?? new OpcionesDeSeguridad();

        return new OpcionesDeSeguridad
        {
            Habilitada = base_.Habilitada,
            Emisor = base_.Emisor ?? configuracion["URL_KEYCLOAK"],
            Audiencia = base_.Audiencia ?? configuracion["ID_CLIENTE_KEYCLOAK"],
            ClaimPermisos = base_.ClaimPermisos,
            ClaimCorreo = base_.ClaimCorreo,
        };
    }
}
