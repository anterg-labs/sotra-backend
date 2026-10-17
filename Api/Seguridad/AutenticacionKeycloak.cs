using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Api.Seguridad;

// Valida los tokens que emite Keycloak para el realm sotra.
public static class AutenticacionKeycloak
{
    private const string ReclamoDeRolesDelRealm = "realm_access";
    private const string ReclamoDeNombreDeUsuario = "preferred_username";

    public static IServiceCollection AgregarAutenticacionKeycloak(this IServiceCollection servicios)
    {
        // El emisor es la URL pública de Keycloak (la que ve el navegador y la que va
        // dentro del token). Los metadatos y las claves de firma se descargan desde
        // KEYCLOAK_METADATA, que dentro de Docker apunta a la red interna (keycloak:8080).
        var emisor = LeerVariable("KEYCLOAK_EMISOR");
        var metadatos = LeerVariable("KEYCLOAK_METADATA");
        var audiencia = LeerVariable("KEYCLOAK_AUDIENCIA");

        servicios
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opciones =>
            {
                opciones.MetadataAddress = metadatos;
                opciones.RequireHttpsMetadata = metadatos.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
                opciones.MapInboundClaims = false;
                opciones.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = emisor,
                    ValidAudience = audiencia,
                    NameClaimType = ReclamoDeNombreDeUsuario,
                    RoleClaimType = ClaimTypes.Role
                };
                opciones.Events = new JwtBearerEvents
                {
                    OnTokenValidated = contexto =>
                    {
                        if (contexto.Principal?.Identity is ClaimsIdentity identidad)
                        {
                            AgregarRolesDelRealm(identidad);
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        return servicios;
    }

    // Keycloak manda los roles del realm dentro de un objeto JSON:
    // "realm_access": { "roles": ["tecnico", ...] }. .NET no lo entiende solo,
    // así que cada rol se agrega como un reclamo de rol para que funcione [Authorize(Roles = "...")].
    private static void AgregarRolesDelRealm(ClaimsIdentity identidad)
    {
        var reclamo = identidad.FindFirst(ReclamoDeRolesDelRealm);
        if (reclamo is null)
        {
            return;
        }

        using var documento = JsonDocument.Parse(reclamo.Value);
        if (!documento.RootElement.TryGetProperty("roles", out var roles) || roles.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var rol in roles.EnumerateArray())
        {
            var nombreRol = rol.GetString();
            if (!string.IsNullOrWhiteSpace(nombreRol))
            {
                identidad.AddClaim(new Claim(ClaimTypes.Role, nombreRol));
            }
        }
    }

    private static string LeerVariable(string nombre)
    {
        var valor = Environment.GetEnvironmentVariable(nombre);
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new InvalidOperationException($"Falta la variable {nombre}.");
        }

        return valor;
    }
}
