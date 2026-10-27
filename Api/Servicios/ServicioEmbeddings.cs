using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Tokenizers.DotNet;

namespace Api.Servicios;

/// <summary>
/// Calcula los embeddings de bge-m3 dentro de la API con ONNX Runtime, sin servicios externos: así una caída del
/// proveedor de IA no afecta la búsqueda. Usa el vector del primer token normalizado, como bge-m3.
/// </summary>
public class ServicioEmbeddings : IDisposable
{
    public const int DimensionesDelEmbedding = 1024;

    private const int TokensMaximosPorTexto = 1024;

    // De a un texto: en el modelo cuantizado, el relleno de un lote cambia el vector (coseno ~0,98 contra el mismo
    // texto solo). Así la consulta y los fragmentos guardados se calculan siempre igual.
    private const int TextosPorLote = 1;
    private const long TokenDeRelleno = 1;

    private readonly object _bloqueo = new();
    private InferenceSession? _sesion;
    private Tokenizer? _tokenizador;

    private void Preparar()
    {
        lock (_bloqueo)
        {
            if (_sesion is not null && _tokenizador is not null)
            {
                return;
            }

            var rutaModelo = Environment.GetEnvironmentVariable("EMBEDDINGS_MODELO_RUTA");
            var rutaTokenizador = Environment.GetEnvironmentVariable("EMBEDDINGS_TOKENIZADOR_RUTA");

            if (string.IsNullOrWhiteSpace(rutaModelo) || !File.Exists(rutaModelo))
            {
                throw new ModeloLocalNoDisponibleException("No se encontró el modelo de embeddings.");
            }

            if (string.IsNullOrWhiteSpace(rutaTokenizador) || !File.Exists(rutaTokenizador))
            {
                throw new ModeloLocalNoDisponibleException("No se encontró el tokenizador de embeddings.");
            }

            _tokenizador = new Tokenizer(rutaTokenizador);
            _sesion = new InferenceSession(rutaModelo);
        }
    }

    /// <summary>Carga el modelo si hace falta; lanza <see cref="ModeloLocalNoDisponibleException"/> si no está.</summary>
    public virtual void VerificarDisponible()
    {
        Preparar();
    }

    public virtual List<float[]> Crear(List<string> textos)
    {
        Preparar();
        var vectores = new List<float[]>();
        for (var inicio = 0; inicio < textos.Count; inicio += TextosPorLote)
        {
            var lote = textos.GetRange(inicio, Math.Min(TextosPorLote, textos.Count - inicio));
            var secuencias = new List<long[]>();
            foreach (var texto in lote)
            {
                secuencias.Add(Tokenizar(texto));
            }

            vectores.AddRange(CalcularVectores(_sesion!, secuencias));
        }

        return vectores;
    }

    /// <summary>Recorta los textos largos conservando el token de cierre, que el modelo espera al final.</summary>
    private long[] Tokenizar(string texto)
    {
        var tokens = _tokenizador!.Encode(texto);
        if (tokens.Length <= TokensMaximosPorTexto)
        {
            var completos = new long[tokens.Length];
            for (var posicion = 0; posicion < tokens.Length; posicion++)
            {
                completos[posicion] = tokens[posicion];
            }

            return completos;
        }

        var recortados = new long[TokensMaximosPorTexto];
        for (var posicion = 0; posicion < TokensMaximosPorTexto - 1; posicion++)
        {
            recortados[posicion] = tokens[posicion];
        }

        recortados[TokensMaximosPorTexto - 1] = tokens[tokens.Length - 1];
        return recortados;
    }

    private static List<float[]> CalcularVectores(InferenceSession sesion, List<long[]> secuencias)
    {
        var cantidadDeFilas = secuencias.Count;
        var largoMaximo = 0;
        foreach (var secuencia in secuencias)
        {
            largoMaximo = Math.Max(largoMaximo, secuencia.Length);
        }

        var identificadores = new DenseTensor<long>(new[] { cantidadDeFilas, largoMaximo });
        var mascara = new DenseTensor<long>(new[] { cantidadDeFilas, largoMaximo });
        for (var fila = 0; fila < cantidadDeFilas; fila++)
        {
            for (var columna = 0; columna < largoMaximo; columna++)
            {
                var esToken = columna < secuencias[fila].Length;
                identificadores[fila, columna] = esToken ? secuencias[fila][columna] : TokenDeRelleno;
                mascara[fila, columna] = esToken ? 1 : 0;
            }
        }

        var entradas = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", identificadores),
            NamedOnnxValue.CreateFromTensor("attention_mask", mascara)
        };

        // Algunas exportaciones de XLM-RoBERTa piden también token_type_ids, siempre en cero.
        if (sesion.InputMetadata.ContainsKey("token_type_ids"))
        {
            entradas.Add(NamedOnnxValue.CreateFromTensor("token_type_ids",
                new DenseTensor<long>(new[] { cantidadDeFilas, largoMaximo })));
        }

        using var salidas = sesion.Run(entradas);
        var estados = salidas[0].AsTensor<float>();
        var vectores = new List<float[]>();
        for (var fila = 0; fila < cantidadDeFilas; fila++)
        {
            var vector = new float[DimensionesDelEmbedding];
            for (var dimension = 0; dimension < DimensionesDelEmbedding; dimension++)
            {
                vector[dimension] = estados.Rank == 3 ? estados[fila, 0, dimension] : estados[fila, dimension];
            }

            vectores.Add(Normalizar(vector));
        }

        return vectores;
    }

    /// <summary>Lleva el vector a largo 1; un vector inválido es un error del modelo, no un resultado posible.</summary>
    public static float[] Normalizar(float[] vector)
    {
        if (vector.Length != DimensionesDelEmbedding)
        {
            throw new InvalidDataException("EMBEDDING_INVALIDO");
        }

        double sumaDeCuadrados = 0;
        foreach (var valor in vector)
        {
            if (!float.IsFinite(valor))
            {
                throw new InvalidDataException("EMBEDDING_INVALIDO");
            }

            sumaDeCuadrados += valor * valor;
        }

        if (sumaDeCuadrados == 0)
        {
            throw new InvalidDataException("EMBEDDING_INVALIDO");
        }

        var largo = (float)Math.Sqrt(sumaDeCuadrados);
        var normalizado = new float[vector.Length];
        for (var posicion = 0; posicion < vector.Length; posicion++)
        {
            normalizado[posicion] = vector[posicion] / largo;
        }

        return normalizado;
    }

    public void Dispose()
    {
        _sesion?.Dispose();
        _tokenizador?.Dispose();
    }
}
