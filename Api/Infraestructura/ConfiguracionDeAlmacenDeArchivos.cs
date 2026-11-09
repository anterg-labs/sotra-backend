using Api.Servicios;

namespace Api.Infraestructura;

public static class ConfiguracionDeAlmacenDeArchivos
{
    private static readonly string[] Variables =
        ["R2_ENDPOINT", "R2_BUCKET", "R2_ACCESS_KEY_ID", "R2_SECRET_ACCESS_KEY"];

    /// <summary>
    /// Registra Cloudflare R2 como almacén de los archivos de las evidencias cuando están las cuatro variables
    /// R2_*. Sin ninguna, los archivos se siguen guardando en la base (desarrollo local y pruebas). Con algunas sí y
    /// otras no, la API no arranca, para no guardar archivos en un lugar distinto del esperado sin avisar.
    /// </summary>
    public static IServiceCollection AgregarAlmacenDeArchivos(this IServiceCollection servicios)
    {
        var valores = new Dictionary<string, string>();
        var faltantes = new List<string>();
        foreach (var variable in Variables)
        {
            var valor = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(valor))
            {
                faltantes.Add(variable);
            }
            else
            {
                valores[variable] = valor.Trim();
            }
        }

        if (faltantes.Count == Variables.Length)
        {
            return servicios;
        }

        if (faltantes.Count > 0)
        {
            throw new InvalidOperationException(
                "Faltan variables de Cloudflare R2 en el archivo .env: " + string.Join(", ", faltantes) + ".");
        }

        servicios.AddSingleton<IAlmacenDeArchivos>(new AlmacenR2(valores["R2_ENDPOINT"], valores["R2_BUCKET"],
            valores["R2_ACCESS_KEY_ID"], valores["R2_SECRET_ACCESS_KEY"]));
        servicios.AddHostedService<MigradorDeEvidenciasAlAlmacen>();
        return servicios;
    }
}
