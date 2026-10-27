using System.Text.Json;

namespace ComparadorIa;

public class CasoDeEvaluacion
{
    public string Consulta { get; set; } = "";

    /// <summary>Parte del título de la sección esperada; si falta, se elige la sección con más palabras en común.</summary>
    public string? Seccion { get; set; }

    /// <summary>Valores del manual que el resumen debería conservar tal cual (por ejemplo "ISO VG 46").</summary>
    public List<string> ValoresEsperados { get; set; } = new();
}

public class ConfiguracionDeProveedor
{
    public string Nombre { get; set; } = "";

    /// <summary>"openai" para las APIs con formato compatible con OpenAI o "anthropic" para el SDK oficial de Claude.</summary>
    public string Tipo { get; set; } = "openai";

    public string UrlBase { get; set; } = "";
    public string Modelo { get; set; } = "";

    /// <summary>Variable de entorno con la clave; vacía para servicios sin clave como Ollama.</summary>
    public string? VariableClave { get; set; }

    public bool Habilitado { get; set; } = true;
    public int TokensMaximos { get; set; } = 1024;
    public string? Esfuerzo { get; set; }
    public decimal PrecioEntradaPorMillon { get; set; }
    public decimal PrecioSalidaPorMillon { get; set; }
    public Dictionary<string, JsonElement> ParametrosExtra { get; set; } = new();
}

public static class Configuracion
{
    private static readonly JsonSerializerOptions OpcionesDeLectura = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static async Task<List<CasoDeEvaluacion>> LeerCasosAsync(string ruta)
    {
        await using var archivo = File.OpenRead(ruta);
        var casos = await JsonSerializer.DeserializeAsync<List<CasoDeEvaluacion>>(archivo, OpcionesDeLectura);
        if (casos is null || casos.Count == 0)
        {
            throw new InvalidDataException("El archivo de casos está vacío: " + ruta);
        }

        return casos;
    }

    public static async Task<List<ConfiguracionDeProveedor>> LeerProveedoresAsync(string ruta)
    {
        await using var archivo = File.OpenRead(ruta);
        var proveedores = await JsonSerializer.DeserializeAsync<List<ConfiguracionDeProveedor>>(archivo, OpcionesDeLectura);
        if (proveedores is null)
        {
            throw new InvalidDataException("El archivo de proveedores está vacío: " + ruta);
        }

        return proveedores;
    }
}
