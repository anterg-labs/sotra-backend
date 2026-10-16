using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Api.Infraestructura;

public static class ProxiesDeConfianza
{
    /// <summary>
    /// Lee la variable PROXIES_CONFIANZA (direcciones o redes CIDR separadas por coma) y solo acepta
    /// las cabeceras X-Forwarded-* de esos proxies. Sin la variable, solo se confía en el bucle local.
    /// </summary>
    public static IServiceCollection AgregarProxiesDeConfianza(
        this IServiceCollection servicios, IConfiguration configuracion)
    {
        var valor = configuracion["PROXIES_CONFIANZA"] ?? Environment.GetEnvironmentVariable("PROXIES_CONFIANZA");

        servicios.Configure<ForwardedHeadersOptions>(opciones =>
        {
            opciones.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            opciones.ForwardLimit = 1;

            foreach (var entrada in (valor ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                AgregarEntrada(opciones, entrada);
            }
        });

        return servicios;
    }

    private static void AgregarEntrada(ForwardedHeadersOptions opciones, string entrada)
    {
        var partes = entrada.Split('/', 2);
        if (!IPAddress.TryParse(partes[0], out var direccion))
        {
            throw new InvalidOperationException("PROXIES_CONFIANZA contiene una dirección que no es válida.");
        }

        if (partes.Length == 1)
        {
            opciones.KnownProxies.Add(direccion);
            return;
        }

        if (!int.TryParse(partes[1], out var prefijo)
            || prefijo < 0
            || prefijo > (direccion.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128))
        {
            throw new InvalidOperationException("PROXIES_CONFIANZA contiene una red con un prefijo que no es válido.");
        }

        opciones.KnownIPNetworks.Add(new System.Net.IPNetwork(direccion, prefijo));
    }
}
