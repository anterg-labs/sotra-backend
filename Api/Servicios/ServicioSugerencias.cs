using System.Text.RegularExpressions;
using Npgsql;
using Pgvector;

namespace Api.Servicios;

public class ServicioSugerencias : IAsyncDisposable
{
    private const int ResultadosPorTipoDeBusqueda = 30;
    private const int ConstanteDeFusionDeRankings = 60;
    private const double DistanciaMaximaPermitida = 0.45;
    private const int MaximoDeFragmentosRecuperados = 18;
    private const int MaximoDePalabrasEnBusquedaTextual = 12;
    private const string OperadorOEnBusquedaTextual = " | ";
    private const float MargenDePuntajeParaSeccionesRelevantes = 3;
    private const int MaximoDeSeccionesRelevantes = 8;
    private const string PedidoParaElReranker =
        "Procedimiento, advertencia o especificación para realizar esta tarea de mantenimiento: ";

    private static readonly Regex PatronPalabraDeBusqueda = new("[a-záéíóúñ0-9]+");

    private readonly ServicioOllama _ollama;
    private readonly ServicioReranker _reranker;
    private readonly NpgsqlDataSource _baseDatos;

    public ServicioSugerencias(ServicioOllama ollama, ServicioReranker reranker)
    {
        _ollama = ollama;
        _reranker = reranker;
        _baseDatos = BaseVectorial.CrearConexion();
    }

    public async Task<string?> BuscarAsync(string consulta, int numero, CancellationToken cancellationToken)
    {
        await BaseVectorial.PrepararTablasAsync(_baseDatos, cancellationToken);

        var fragmentos = await RecuperarFragmentosAsync(consulta, cancellationToken);
        if (fragmentos.Count == 0)
        {
            return null;
        }

        var secciones = SeccionesCandidatas.Armar(fragmentos);
        if (secciones.Count == 0)
        {
            return null;
        }

        var relevantes = OrdenarPorRelevancia(consulta, secciones);
        if (relevantes.Count == 0)
        {
            return null;
        }

        var opciones = SeccionesCandidatas.BuscarOpcionesEquivalentes(consulta, relevantes);
        var elegida = ElegirSeccion(relevantes, opciones, numero);
        elegida = await CompletarConFragmentoSiguienteAsync(elegida, cancellationToken);

        var advertencias = AdvertenciasDelManual.Extraer(elegida.Cuerpo);
        var textoParaResumir = AdvertenciasDelManual.Quitar(elegida.Cuerpo);
        if (textoParaResumir.Length < SeccionesCandidatas.LargoMinimoDeSeccion)
        {
            textoParaResumir = elegida.Cuerpo;
        }

        var resumen = await ResumirAsync(consulta, elegida, textoParaResumir, cancellationToken);
        return RedaccionDeSugerencia.ArmarRespuesta(resumen, advertencias, elegida, opciones);
    }

    private async Task<List<FuenteDocumento>> RecuperarFragmentosAsync(string consulta,
        CancellationToken cancellationToken)
    {
        var textosParaVectorizar = new List<string> { consulta };
        var vectores = await _ollama.CrearEmbeddingsAsync(textosParaVectorizar, cancellationToken);
        var vectorDeLaConsulta = new Vector(vectores[0]);

        var idsPorParecido = await BuscarIdsPorParecidoAsync(vectorDeLaConsulta, cancellationToken);
        var idsPorPalabras = await BuscarIdsPorPalabrasAsync(consulta, cancellationToken);
        var puntajes = CombinarRankings(idsPorParecido, idsPorPalabras);

        var idsCandidatos = new List<long>();
        foreach (var id in puntajes.Keys)
        {
            idsCandidatos.Add(id);
        }

        var candidatos = await LeerFragmentosCercanosAsync(idsCandidatos, vectorDeLaConsulta, cancellationToken);
        candidatos.Sort((primero, segundo) => CompararPorPuntaje(primero, segundo, puntajes));

        var fragmentos = new List<FuenteDocumento>();
        foreach (var candidato in candidatos)
        {
            if (fragmentos.Count == MaximoDeFragmentosRecuperados)
            {
                break;
            }

            fragmentos.Add(candidato);
        }

        return fragmentos;
    }

    private async Task<List<long>> BuscarIdsPorParecidoAsync(Vector vectorDeLaConsulta,
        CancellationToken cancellationToken)
    {
        await using var comando = _baseDatos.CreateCommand("""
            SELECT id
            FROM vectorial.fragmentos
            WHERE documento_id NOT IN (
                SELECT documento_vectorial_id FROM vectorial.documento_tecnico
                WHERE NOT vigente AND documento_vectorial_id IS NOT NULL)
            ORDER BY embedding <=> $1, id
            LIMIT $2
            """);
        comando.Parameters.AddWithValue(vectorDeLaConsulta);
        comando.Parameters.AddWithValue(ResultadosPorTipoDeBusqueda);

        var ids = new List<long>();
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            ids.Add(lector.GetInt64(0));
        }

        return ids;
    }

    internal async Task<List<long>> BuscarIdsPorPalabrasAsync(string consulta, CancellationToken cancellationToken)
    {
        await using var comando = _baseDatos.CreateCommand("""
            SELECT id
            FROM vectorial.fragmentos
            WHERE texto_busqueda @@ to_tsquery('spanish', $1)
              AND documento_id NOT IN (
                SELECT documento_vectorial_id FROM vectorial.documento_tecnico
                WHERE NOT vigente AND documento_vectorial_id IS NOT NULL)
            ORDER BY ts_rank_cd(texto_busqueda, to_tsquery('spanish', $1)) DESC, id
            LIMIT $2
            """);
        comando.Parameters.AddWithValue(CrearBusquedaTextual(consulta));
        comando.Parameters.AddWithValue(ResultadosPorTipoDeBusqueda);

        var ids = new List<long>();
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            ids.Add(lector.GetInt64(0));
        }

        return ids;
    }

    private static Dictionary<long, double> CombinarRankings(List<long> idsPorParecido, List<long> idsPorPalabras)
    {
        var puntajes = new Dictionary<long, double>();
        SumarPuntajesDelRanking(puntajes, idsPorParecido);
        SumarPuntajesDelRanking(puntajes, idsPorPalabras);
        return puntajes;
    }

    private static void SumarPuntajesDelRanking(Dictionary<long, double> puntajes, List<long> idsOrdenados)
    {
        for (var indice = 0; indice < idsOrdenados.Count; indice++)
        {
            var id = idsOrdenados[indice];
            var posicion = indice + 1;
            var puntajeDeEstaPosicion = 1.0 / (ConstanteDeFusionDeRankings + posicion);

            if (!puntajes.ContainsKey(id))
            {
                puntajes[id] = 0;
            }

            puntajes[id] = puntajes[id] + puntajeDeEstaPosicion;
        }
    }

    private async Task<List<FuenteDocumento>> LeerFragmentosCercanosAsync(List<long> ids,
        Vector vectorDeLaConsulta, CancellationToken cancellationToken)
    {
        await using var comando = _baseDatos.CreateCommand("""
            SELECT fragmentos.id, fragmentos.pagina, fragmentos.texto, documentos.nombre
            FROM vectorial.fragmentos AS fragmentos
            JOIN vectorial.documentos AS documentos ON documentos.id = fragmentos.documento_id
            WHERE fragmentos.id = ANY($1)
              AND fragmentos.embedding <=> $2 <= $3
            """);
        comando.Parameters.AddWithValue(ids.ToArray());
        comando.Parameters.AddWithValue(vectorDeLaConsulta);
        comando.Parameters.AddWithValue(DistanciaMaximaPermitida);

        var fragmentos = new List<FuenteDocumento>();
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            var id = lector.GetInt64(0);
            var pagina = lector.GetInt32(1);
            var texto = lector.GetString(2);
            var documento = lector.GetString(3);
            fragmentos.Add(new FuenteDocumento(id, pagina, texto, documento));
        }

        return fragmentos;
    }

    private static int CompararPorPuntaje(FuenteDocumento primero, FuenteDocumento segundo,
        Dictionary<long, double> puntajes)
    {
        var puntajePrimero = puntajes[primero.Id];
        var puntajeSegundo = puntajes[segundo.Id];
        if (puntajePrimero > puntajeSegundo)
        {
            return -1;
        }

        if (puntajePrimero < puntajeSegundo)
        {
            return 1;
        }

        return primero.Id.CompareTo(segundo.Id);
    }

    private List<SeccionDocumento> OrdenarPorRelevancia(string consulta, List<SeccionDocumento> secciones)
    {
        var textosParaOrdenar = new List<FuenteDocumento>();
        for (var posicion = 0; posicion < secciones.Count; posicion++)
        {
            var seccion = secciones[posicion];
            var textoDeLaSeccion = seccion.Titulo + ". " + seccion.Contexto + " " + seccion.Cuerpo;
            textosParaOrdenar.Add(new FuenteDocumento(posicion, seccion.Pagina, textoDeLaSeccion, seccion.Documento));
        }

        var resultados = _reranker.Ordenar(PedidoParaElReranker + consulta, textosParaOrdenar);
        var relevantes = new List<SeccionDocumento>();
        if (resultados.Count == 0 || resultados[0].Puntaje < 0)
        {
            return relevantes;
        }

        var puntajeMinimo = resultados[0].Puntaje - MargenDePuntajeParaSeccionesRelevantes;
        foreach (var resultado in resultados)
        {
            if (resultado.Puntaje < puntajeMinimo || relevantes.Count == MaximoDeSeccionesRelevantes)
            {
                break;
            }

            var posicionDeLaSeccion = (int)resultado.Fuente.Id;
            relevantes.Add(secciones[posicionDeLaSeccion]);
        }

        return relevantes;
    }

    private static SeccionDocumento ElegirSeccion(List<SeccionDocumento> relevantes,
        List<SeccionDocumento> opciones, int numero)
    {
        if (numero < 1)
        {
            numero = 1;
        }

        var listaParaElegir = relevantes;
        if (opciones.Count > 1)
        {
            listaParaElegir = opciones;
        }

        var posicionElegida = (numero - 1) % listaParaElegir.Count;
        return listaParaElegir[posicionElegida];
    }

    private async Task<SeccionDocumento> CompletarConFragmentoSiguienteAsync(SeccionDocumento seccion,
        CancellationToken cancellationToken)
    {
        if (!seccion.LlegaAlFinal)
        {
            return seccion;
        }

        await using var comando = _baseDatos.CreateCommand("""
            SELECT siguiente.id, siguiente.pagina, siguiente.texto, documentos.nombre
            FROM vectorial.fragmentos AS actual
            JOIN vectorial.fragmentos AS siguiente
                ON siguiente.documento_id = actual.documento_id
                AND siguiente.posicion = actual.posicion + 1
            JOIN vectorial.documentos AS documentos ON documentos.id = siguiente.documento_id
            WHERE actual.id = $1
            """);
        comando.Parameters.AddWithValue(seccion.FragmentoId);
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        if (!await lector.ReadAsync(cancellationToken))
        {
            return seccion;
        }

        var fragmentoSiguiente = new FuenteDocumento(lector.GetInt64(0), lector.GetInt32(1),
            lector.GetString(2), lector.GetString(3));
        var seccionesSiguientes = TextoDelManual.DividirEnSecciones(fragmentoSiguiente);
        if (seccionesSiguientes.Count == 0 || seccionesSiguientes[0].Titulo.Length > 0)
        {
            return seccion;
        }

        var cuerpoCompleto = TextoDelManual.UnirContinuacion(seccion.Cuerpo, seccionesSiguientes[0].Cuerpo);
        var seccionCompleta = new SeccionDocumento(seccion.FragmentoId, seccion.Pagina, seccion.Documento,
            seccion.Titulo, seccion.Contexto, cuerpoCompleto, seccion.LlegaAlFinal);
        return seccionCompleta;
    }

    private async Task<string> ResumirAsync(string consulta, SeccionDocumento seccion,
        string textoParaResumir, CancellationToken cancellationToken)
    {
        var resumenSinQwen = TextoDelManual.CrearResumenExtractivo(textoParaResumir);
        var mensaje = RedaccionDeSugerencia.CrearMensajeParaQwen(consulta, seccion, textoParaResumir);

        string resumenDeQwen;
        try
        {
            resumenDeQwen = await _ollama.GenerarTextoAsync(
                RedaccionDeSugerencia.InstruccionesParaQwen, mensaje, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return resumenSinQwen;
        }

        if (!RedaccionDeSugerencia.EsResumenConfiable(resumenDeQwen, consulta, seccion))
        {
            return resumenSinQwen;
        }

        return resumenDeQwen;
    }

    private static string CrearBusquedaTextual(string consulta)
    {
        var palabras = new List<string>();
        foreach (Match coincidencia in PatronPalabraDeBusqueda.Matches(consulta.ToLowerInvariant()))
        {
            var palabra = coincidencia.Value;
            if (palabra.Length >= 3 && !palabras.Contains(palabra))
            {
                palabras.Add(palabra);
            }

            if (palabras.Count == MaximoDePalabrasEnBusquedaTextual)
            {
                break;
            }
        }

        if (palabras.Count == 0)
        {
            return "documentacion";
        }

        return string.Join(OperadorOEnBusquedaTextual, palabras);
    }

    public async ValueTask DisposeAsync()
    {
        await _baseDatos.DisposeAsync();
    }
}
