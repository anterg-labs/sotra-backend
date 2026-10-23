using System.Net.Http.Json;
using System.Text.Json;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Api.Servicios;

public class ServicioOllama
{
    private const string ModeloDeEmbeddings = "bge-m3";
    private const string ModeloDeGeneracion = "qwen2.5:7b";
    private const int DimensionesDelEmbedding = 1024;
    private const string TiempoQueElModeloQuedaCargado = "1h";
    private const int TamanoDeContextoDeQwen = 4096;
    private const int TokensMaximosDeRespuesta = 250;

    private readonly HttpClient _cliente;

    public ServicioOllama(HttpClient cliente)
    {
        _cliente = cliente;
    }

    private async Task<HttpResponseMessage> PostAsync<T>(string ruta, T cuerpo, CancellationToken cancellationToken)
    {
        try
        {
            return await _cliente.PostAsJsonAsync(ruta, cuerpo, cancellationToken);
        }
        catch (Exception ex) when (ex is BrokenCircuitException or TimeoutRejectedException)
        {
            throw new HttpRequestException("Ollama no está disponible por ahora.", ex);
        }
    }

    public async Task<List<float[]>> CrearEmbeddingsAsync(List<string> textos,
        CancellationToken cancellationToken)
    {
        using var respuesta = await PostAsync("/api/embed", new
        {
            model = ModeloDeEmbeddings,
            input = textos,
            truncate = false,
            keep_alive = TiempoQueElModeloQuedaCargado
        }, cancellationToken);

        respuesta.EnsureSuccessStatusCode();
        using var contenido = JsonDocument.Parse(
            await respuesta.Content.ReadAsStringAsync(cancellationToken));

        var vectores = new List<float[]>();
        foreach (var elemento in contenido.RootElement.GetProperty("embeddings").EnumerateArray())
        {
            var numeros = new List<float>();
            foreach (var numero in elemento.EnumerateArray())
            {
                numeros.Add(numero.GetSingle());
            }

            if (!EsVectorValido(numeros))
            {
                throw new InvalidDataException("EMBEDDING_INVALIDO");
            }

            vectores.Add(numeros.ToArray());
        }

        if (vectores.Count != textos.Count)
        {
            throw new InvalidDataException("CANTIDAD_EMBEDDINGS_INVALIDA");
        }

        return vectores;
    }

    public async Task<string> GenerarTextoAsync(string instrucciones, string mensaje,
        CancellationToken cancellationToken)
    {
        using var respuesta = await PostAsync("/api/chat", new
        {
            model = ModeloDeGeneracion,
            stream = false,
            keep_alive = TiempoQueElModeloQuedaCargado,
            options = new
            {
                temperature = 0,
                num_ctx = TamanoDeContextoDeQwen,
                num_predict = TokensMaximosDeRespuesta
            },
            messages = new[]
            {
                new { role = "system", content = instrucciones },
                new { role = "user", content = mensaje }
            }
        }, cancellationToken);

        respuesta.EnsureSuccessStatusCode();
        using var contenido = JsonDocument.Parse(
            await respuesta.Content.ReadAsStringAsync(cancellationToken));

        var texto = contenido.RootElement.GetProperty("message").GetProperty("content").GetString();
        if (texto is null)
        {
            return "";
        }

        return texto.Trim();
    }

    private static bool EsVectorValido(List<float> numeros)
    {
        if (numeros.Count != DimensionesDelEmbedding)
        {
            return false;
        }

        var todosSonCero = true;
        foreach (var numero in numeros)
        {
            if (!float.IsFinite(numero))
            {
                return false;
            }

            if (numero != 0)
            {
                todosSonCero = false;
            }
        }

        return !todosSonCero;
    }
}
