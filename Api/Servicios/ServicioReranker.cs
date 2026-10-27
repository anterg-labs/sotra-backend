using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Tokenizers.DotNet;

namespace Api.Servicios;

public class ServicioReranker : IDisposable
{
    private const long TokenDeInicio = 0;
    private const long TokenSeparador = 2;
    private const int TokensMaximosPorPar = 512;
    private const int TokensMaximosDeConsulta = 64;
    private const int TokensEspecialesPorPar = 4;
    private const int ParesPorLote = 4;

    private InferenceSession? _sesion;
    private Tokenizer? _tokenizador;

    public ServicioReranker()
    {
    }

    private void Preparar()
    {
        if (_sesion is not null && _tokenizador is not null)
        {
            return;
        }

        var rutaModelo = Environment.GetEnvironmentVariable("RERANKER_MODELO_RUTA");
        var rutaTokenizador = Environment.GetEnvironmentVariable("RERANKER_TOKENIZADOR_RUTA");

        if (string.IsNullOrWhiteSpace(rutaModelo) || !File.Exists(rutaModelo))
        {
            throw new ModeloLocalNoDisponibleException("No se encontró el modelo del reranker.");
        }

        if (string.IsNullOrWhiteSpace(rutaTokenizador) || !File.Exists(rutaTokenizador))
        {
            throw new ModeloLocalNoDisponibleException("No se encontró el tokenizador del reranker.");
        }

        _tokenizador = new Tokenizer(rutaTokenizador);
        _sesion = new InferenceSession(rutaModelo);
    }

    public List<FuentePuntuada> Ordenar(string consulta, List<FuenteDocumento> fuentes)
    {
        Preparar();
        if (_sesion is null || _tokenizador is null)
        {
            throw new InvalidOperationException("No se pudo preparar el reranker.");
        }

        var tokensConsulta = ObtenerTokensSinExtremos(consulta, TokensMaximosDeConsulta);
        var tokensMaximosDeTexto = TokensMaximosPorPar - tokensConsulta.Count - TokensEspecialesPorPar;
        var resultados = new List<FuentePuntuada>();

        var lotes = ArmarLotes(fuentes);
        foreach (var lote in lotes)
        {
            var secuencias = new List<long[]>();
            foreach (var fuente in lote)
            {
                var tokensTexto = ObtenerTokensSinExtremos(fuente.Texto, tokensMaximosDeTexto);
                var secuencia = ArmarSecuencia(tokensConsulta, tokensTexto);
                secuencias.Add(secuencia);
            }

            var puntajes = CalcularPuntajes(_sesion, secuencias);
            for (var posicion = 0; posicion < lote.Count; posicion++)
            {
                resultados.Add(new FuentePuntuada(lote[posicion], puntajes[posicion]));
            }
        }

        resultados.Sort(CompararDeMayorAMenorPuntaje);
        return resultados;
    }

    private static List<List<FuenteDocumento>> ArmarLotes(List<FuenteDocumento> fuentes)
    {
        var lotes = new List<List<FuenteDocumento>>();
        var loteActual = new List<FuenteDocumento>();
        foreach (var fuente in fuentes)
        {
            loteActual.Add(fuente);
            if (loteActual.Count == ParesPorLote)
            {
                lotes.Add(loteActual);
                loteActual = new List<FuenteDocumento>();
            }
        }

        if (loteActual.Count > 0)
        {
            lotes.Add(loteActual);
        }

        return lotes;
    }

    private static long[] ArmarSecuencia(List<long> tokensConsulta, List<long> tokensTexto)
    {
        var secuencia = new List<long>();
        secuencia.Add(TokenDeInicio);
        secuencia.AddRange(tokensConsulta);
        secuencia.Add(TokenSeparador);
        secuencia.Add(TokenSeparador);
        secuencia.AddRange(tokensTexto);
        secuencia.Add(TokenSeparador);
        return secuencia.ToArray();
    }

    private static float[] CalcularPuntajes(InferenceSession sesion, List<long[]> secuencias)
    {
        var cantidadDeFilas = secuencias.Count;
        var largoMaximo = 0;
        foreach (var secuencia in secuencias)
        {
            if (secuencia.Length > largoMaximo)
            {
                largoMaximo = secuencia.Length;
            }
        }

        var identificadores = new DenseTensor<long>(new[] { cantidadDeFilas, largoMaximo });
        var mascara = new DenseTensor<long>(new[] { cantidadDeFilas, largoMaximo });
        for (var fila = 0; fila < cantidadDeFilas; fila++)
        {
            for (var columna = 0; columna < secuencias[fila].Length; columna++)
            {
                identificadores[fila, columna] = secuencias[fila][columna];
                mascara[fila, columna] = 1;
            }
        }

        var entradas = new List<NamedOnnxValue>();
        entradas.Add(NamedOnnxValue.CreateFromTensor("input_ids", identificadores));
        entradas.Add(NamedOnnxValue.CreateFromTensor("attention_mask", mascara));

        using var salidas = sesion.Run(entradas);
        var tensorDePuntajes = salidas[0].AsTensor<float>();
        return tensorDePuntajes.ToArray();
    }

    private static int CompararDeMayorAMenorPuntaje(FuentePuntuada primero, FuentePuntuada segundo)
    {
        if (primero.Puntaje > segundo.Puntaje)
        {
            return -1;
        }

        if (primero.Puntaje < segundo.Puntaje)
        {
            return 1;
        }

        return primero.Fuente.Id.CompareTo(segundo.Fuente.Id);
    }

    private List<long> ObtenerTokensSinExtremos(string texto, int cantidadMaxima)
    {
        var todosLosTokens = _tokenizador!.Encode(texto);
        var tokens = new List<long>();
        for (var posicion = 1; posicion < todosLosTokens.Length - 1; posicion++)
        {
            if (tokens.Count == cantidadMaxima)
            {
                break;
            }
            tokens.Add(todosLosTokens[posicion]);
        }

        return tokens;
    }

    public void Dispose()
    {
        if (_sesion is not null)
        {
            _sesion.Dispose();
        }

        if (_tokenizador is not null)
        {
            _tokenizador.Dispose();
        }
    }
}
