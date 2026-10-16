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
            ["OLLAMA_BASE_URL"] = "http://localhost:11434",
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
