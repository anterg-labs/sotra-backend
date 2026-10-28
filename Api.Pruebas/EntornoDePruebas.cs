using NUnit.Framework;
using Testcontainers.PostgreSql;

namespace Api.Pruebas;

[SetUpFixture]
public class EntornoDePruebas
{
    private const string ImagenPostgres = "pgvector/pgvector:pg18";
    private const string NombreBase = "sotra";
    private const string UsuarioBase = "sotra_pruebas";
    private const string ContrasenaBase = "contrasena_solo_para_pruebas";
    private const int PuertoPostgres = 5432;

    private static string _puertoDelContenedor = "";

    private PostgreSqlContainer? _contenedor;
    private string _carpetaTemporal = "";
    private string _carpetaOriginal = "";

    public static FabricaApi Fabrica { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task IniciarAsync()
    {
        var scriptOperativo = Path.Combine(AppContext.BaseDirectory, "init", "01-init.sql");
        var scriptVectorial = Path.Combine(AppContext.BaseDirectory, "init", "02-vectorial.sql");

        _contenedor = new PostgreSqlBuilder(ImagenPostgres)
            .WithDatabase(NombreBase)
            .WithUsername(UsuarioBase)
            .WithPassword(ContrasenaBase)
            .WithEnvironment("POSTGRES_INITDB_ARGS", "--locale=C.utf8 --locale-provider=icu --icu-locale=es-UY")
            .WithResourceMapping(await File.ReadAllBytesAsync(scriptOperativo), "/docker-entrypoint-initdb.d/01-init.sql")
            .WithResourceMapping(await File.ReadAllBytesAsync(scriptVectorial), "/docker-entrypoint-initdb.d/02-vectorial.sql")
            .Build();

        await _contenedor.StartAsync();

        var host = _contenedor.Hostname;
        var puerto = _contenedor.GetMappedPublicPort(PuertoPostgres).ToString();
        var variables = new Dictionary<string, string>
        {
            ["DB_HOST"] = host,
            ["DB_PORT"] = puerto,
            ["DB_NAME"] = NombreBase,
            ["DB_USER"] = UsuarioBase,
            ["DB_PASSWORD"] = ContrasenaBase,
            ["FRONTEND_ORIGIN"] = "http://localhost",
            // Sin caché de analítica: cada prueba lee los datos que acaba de crear (los avisos no tienen filtros que separen las pruebas).
            ["CacheAnalitica__Segundos"] = "0",
            // Sin modelos locales ni clave del proveedor: las pruebas nunca cargan modelos ni llaman a la API externa,
            // aunque la máquina tenga esas variables definidas. Una variable vacía se elimina del entorno.
            ["EMBEDDINGS_MODELO_RUTA"] = "",
            ["EMBEDDINGS_TOKENIZADOR_RUTA"] = "",
            ["RERANKER_MODELO_RUTA"] = "",
            ["RERANKER_TOKENIZADOR_RUTA"] = "",
            ["IA_API_CLAVE"] = "",
            // La API exige estas variables para validar tokens; las pruebas no levantan Keycloak.
            ["KEYCLOAK_EMISOR"] = "https://keycloak.pruebas.test/realms/sotra",
            ["KEYCLOAK_METADATA"] = "https://keycloak.pruebas.test/realms/sotra/.well-known/openid-configuration",
            ["KEYCLOAK_AUDIENCIA"] = "sotra-api",
        };

        // Program.cs carga el archivo .env que encuentre desde la carpeta actual hacia arriba.
        // Se trabaja desde una carpeta temporal con su propio .env para no leer nunca el .env real
        // del backend, que apunta a la base de desarrollo.
        _carpetaTemporal = Path.Combine(Path.GetTempPath(), "sotra-pruebas-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_carpetaTemporal);
        var lineasEnv = variables.Select(variable => variable.Key + "=" + variable.Value);
        await File.WriteAllLinesAsync(Path.Combine(_carpetaTemporal, ".env"), lineasEnv);

        _carpetaOriginal = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_carpetaTemporal);

        foreach (var variable in variables)
        {
            Environment.SetEnvironmentVariable(variable.Key, variable.Value);
        }

        // Las pruebas de caracterización llaman a la API sin token. Va solo como variable de entorno
        // (no en el .env) para que SeguridadPruebas pueda encender la autenticación.
        Environment.SetEnvironmentVariable("AUTH_OBLIGATORIA", "false");

        // Toda la suite comparte una dirección IP: se sube el límite de solicitudes para que no frene las pruebas.
        // Va solo como variable de entorno (no en el .env) para que una prueba pueda bajarlo.
        foreach (var limite in new[] { "Escrituras", "General", "Subidas", "Sugerencias" })
        {
            Environment.SetEnvironmentVariable("LimiteSolicitudes__PorMinuto" + limite, "100000");
        }

        _puertoDelContenedor = puerto;
        VerificarBaseDePruebas();

        Fabrica = new FabricaApi();
    }

    [OneTimeTearDown]
    public async Task FinalizarAsync()
    {
        if (Fabrica is not null)
        {
            await Fabrica.DisposeAsync();
        }

        if (_carpetaOriginal != "")
        {
            Directory.SetCurrentDirectory(_carpetaOriginal);
        }

        if (_contenedor is not null)
        {
            await _contenedor.DisposeAsync();
        }

        if (_carpetaTemporal != "" && Directory.Exists(_carpetaTemporal))
        {
            Directory.Delete(_carpetaTemporal, recursive: true);
        }
    }

    public static void VerificarBaseDePruebas()
    {
        var puertoEnUso = Environment.GetEnvironmentVariable("DB_PORT");
        var usuarioEnUso = Environment.GetEnvironmentVariable("DB_USER");
        if (puertoEnUso != _puertoDelContenedor || usuarioEnUso != UsuarioBase)
        {
            throw new InvalidOperationException(
                "Las pruebas no apuntan a la base desechable. Se cancelan para no tocar la base de desarrollo.");
        }
    }
}
