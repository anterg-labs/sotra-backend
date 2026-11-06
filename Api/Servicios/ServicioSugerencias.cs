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
    private const float PuntajeMinimoEntreIdiomas = -2.0f;
    private const float PuntajeQueNoNecesitaDistancia = 1.0f;
    private const double DistanciaMaximaEntreIdiomas = 0.40;
    private const string PedidoParaElReranker =
        "Procedimiento, advertencia o especificación para realizar esta tarea de mantenimiento: ";

    // Excluye los documentos obsoletos. Si la orden tiene activo ($3), usa solo la documentación de esa máquina: la
    // del activo, la de otros activos del mismo modelo, la asociada al modelo y la general (sin activo ni modelo).
    // Así una orden de una SANY no cita el manual de otra marca.
    private const string FiltroDeDocumentos = """
        documento_id NOT IN (
            SELECT documento_vectorial_id FROM vectorial.documento_tecnico
            WHERE NOT vigente AND documento_vectorial_id IS NOT NULL)
        AND ($3::bigint IS NULL OR documento_id IN (
            SELECT documento.documento_vectorial_id
            FROM vectorial.documento_tecnico AS documento
            LEFT JOIN operativo.activo AS activo_del_documento
                ON activo_del_documento.id_activo = documento.activo_id
            WHERE documento.documento_vectorial_id IS NOT NULL
              AND ((documento.activo_id IS NULL AND documento.modelo_maquina_id IS NULL)
                OR documento.activo_id = $3
                OR documento.modelo_maquina_id = (SELECT id_modelo_maquina FROM operativo.activo WHERE id_activo = $3)
                OR activo_del_documento.id_modelo_maquina =
                    (SELECT id_modelo_maquina FROM operativo.activo WHERE id_activo = $3))))
        """;

    private static readonly Regex PatronPalabraDeBusqueda = new("[a-záéíóúñ0-9]+");

    // La búsqueda por palabras usa el diccionario español, que no quita estas palabras de una consulta en inglés.
    private static readonly HashSet<string> PalabrasSinValorEnIngles =
        ["the", "and", "for", "with", "from", "this", "that", "into", "per"];

    private readonly ServicioEmbeddings _embeddings;
    private readonly ServicioReranker _reranker;
    private readonly RedactorDeSugerencias _redactor;
    private readonly TraductorDeConsultas _traductor;
    private readonly NpgsqlDataSource _baseDatos;

    public ServicioSugerencias(ServicioEmbeddings embeddings, ServicioReranker reranker,
        RedactorDeSugerencias redactor, TraductorDeConsultas traductor)
    {
        _embeddings = embeddings;
        _reranker = reranker;
        _redactor = redactor;
        _traductor = traductor;
        _baseDatos = BaseVectorial.CrearConexion();
    }

    /// <summary>
    /// Busca la sección del manual que corresponde a la consulta y la resume. Devuelve <c>null</c> si la documentación
    /// no tiene información suficiente. El modo indica si el resumen es del proveedor de IA o el texto del manual.
    /// Con <paramref name="activoId"/> busca solo en la documentación de esa máquina y en la general.
    /// </summary>
    public async Task<SugerenciaRedactada?> BuscarAsync(string consulta, int numero, long? activoId,
        CancellationToken cancellationToken)
    {
        await BaseVectorial.PrepararTablasAsync(_baseDatos, cancellationToken);

        consulta = IntervaloDeHoras.NormalizarMiles(consulta);
        var relevantes = await BuscarSeccionesRelevantesAsync(consulta, null, activoId, cancellationToken);
        if (relevantes.Count == 0)
        {
            // Una consulta corta en español suele no alcanzar un manual en inglés. Solo aquí se traduce: lo que ya se
            // encuentra sigue igual, sin otra llamada al proveedor ni más demora.
            var traduccion = await _traductor.TraducirAsync(consulta, cancellationToken);
            if (traduccion is not null)
            {
                relevantes = await BuscarSeccionesRelevantesAsync(consulta, traduccion, activoId, cancellationToken);
            }
        }

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

        var resumen = await _redactor.ResumirAsync(consulta, elegida, textoParaResumir, cancellationToken);
        if (resumen.Modo == ModosDeSugerencia.SinInformacion)
        {
            return null;
        }

        var respuesta = RedaccionDeSugerencia.ArmarRespuesta(resumen.Texto, advertencias, elegida, opciones,
            resumen.Titulo);
        return new SugerenciaRedactada(respuesta, resumen.Modo);
    }

    private async Task<List<SeccionDocumento>> BuscarSeccionesRelevantesAsync(string consulta, string? traduccion,
        long? activoId, CancellationToken cancellationToken)
    {
        var (fragmentos, distancias) = await RecuperarFragmentosAsync(consulta, traduccion, activoId, cancellationToken);
        var secciones = SeccionesCandidatas.Armar(fragmentos);
        return secciones.Count == 0
            ? new List<SeccionDocumento>()
            : OrdenarPorRelevancia(consulta, traduccion, secciones, distancias);
    }

    /// <summary>
    /// Devuelve los fragmentos candidatos y la distancia a la consulta de los que llegaron por parecido; la usa
    /// <see cref="EsSeccionConfiable"/> cuando el reranker duda. Con traducción se busca con las dos consultas y se
    /// fusionan los cuatro rankings: un fragmento vale por la consulta que más se le acerca.
    /// </summary>
    private async Task<(List<FuenteDocumento> Fragmentos, Dictionary<long, double> Distancias)> RecuperarFragmentosAsync(
        string consulta, string? traduccion, long? activoId, CancellationToken cancellationToken)
    {
        var vectores = _embeddings.Crear(traduccion is null ? [consulta] : [consulta, traduccion]);
        var vectorDeLaConsulta = new Vector(vectores[0]);
        Vector? vectorDeLaTraduccion = traduccion is null ? null : new Vector(vectores[1]);

        var distancias = new Dictionary<long, double>();
        var rankings = new List<List<long>>
        {
            await BuscarIdsPorParecidoAsync(vectorDeLaConsulta, activoId, distancias, cancellationToken)
        };
        var idsPorPalabras = await BuscarIdsPorPalabrasAsync(consulta, activoId, cancellationToken);
        rankings.Add(idsPorPalabras);
        if (traduccion is not null)
        {
            rankings.Add(await BuscarIdsPorParecidoAsync(vectorDeLaTraduccion!, activoId, distancias, cancellationToken));
            var idsPorPalabrasTraducidas = await BuscarIdsPorPalabrasAsync(traduccion, activoId, cancellationToken);
            rankings.Add(idsPorPalabrasTraducidas);
            idsPorPalabras = idsPorPalabras.Union(idsPorPalabrasTraducidas).ToList();
        }

        var puntajes = CombinarRankings(rankings);

        var idsCandidatos = new List<long>();
        foreach (var id in puntajes.Keys)
        {
            idsCandidatos.Add(id);
        }

        var candidatos = await LeerFragmentosCercanosAsync(idsCandidatos, idsPorPalabras, vectorDeLaConsulta,
            cancellationToken, distancias, vectorDeLaTraduccion);
        candidatos.Sort((primero, segundo) => CompararPorPuntaje(primero, segundo, puntajes));

        var fragmentos = new List<FuenteDocumento>();
        foreach (var candidato in candidatos)
        {
            if (fragmentos.Count == MaximoDeFragmentosRecuperados)
            {
                break;
            }

            if (TextoDelManual.EsIndice(candidato.Texto))
            {
                continue;
            }

            fragmentos.Add(candidato);
        }

        return (fragmentos, distancias);
    }

    private async Task<List<long>> BuscarIdsPorParecidoAsync(Vector vectorDeLaConsulta, long? activoId,
        Dictionary<long, double> distancias, CancellationToken cancellationToken)
    {
        await using var comando = _baseDatos.CreateCommand($"""
            SELECT id, embedding <=> $1
            FROM vectorial.fragmentos
            WHERE {FiltroDeDocumentos}
            ORDER BY embedding <=> $1, id
            LIMIT $2
            """);
        comando.Parameters.AddWithValue(vectorDeLaConsulta);
        comando.Parameters.AddWithValue(ResultadosPorTipoDeBusqueda);
        comando.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Bigint, (object?)activoId ?? DBNull.Value);

        var ids = new List<long>();
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            var id = lector.GetInt64(0);
            ids.Add(id);
            var distancia = lector.GetDouble(1);
            distancias[id] = distancias.TryGetValue(id, out var anterior) ? Math.Min(anterior, distancia) : distancia;
        }

        return ids;
    }

    internal async Task<List<long>> BuscarIdsPorPalabrasAsync(string consulta, long? activoId,
        CancellationToken cancellationToken)
    {
        await using var comando = _baseDatos.CreateCommand($"""
            SELECT id
            FROM vectorial.fragmentos
            WHERE texto_busqueda @@ to_tsquery('spanish', $1)
              AND {FiltroDeDocumentos}
            ORDER BY ts_rank_cd(texto_busqueda, to_tsquery('spanish', $1)) DESC, id
            LIMIT $2
            """);
        comando.Parameters.AddWithValue(CrearBusquedaTextual(consulta));
        comando.Parameters.AddWithValue(ResultadosPorTipoDeBusqueda);
        comando.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Bigint, (object?)activoId ?? DBNull.Value);

        var ids = new List<long>();
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            ids.Add(lector.GetInt64(0));
        }

        return ids;
    }

    private static Dictionary<long, double> CombinarRankings(List<List<long>> rankings)
    {
        var puntajes = new Dictionary<long, double>();
        foreach (var ranking in rankings)
        {
            SumarPuntajesDelRanking(puntajes, ranking);
        }

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

    /// <summary>
    /// Lee los candidatos. La distancia máxima solo filtra los que llegaron por parecido: un fragmento que contiene las
    /// palabras de la consulta pasa aunque su vector esté lejos, porque un fragmento largo que mezcla varios
    /// procedimientos queda lejos de cualquier consulta. El reranker decide después si de verdad responde.
    /// </summary>
    internal async Task<List<FuenteDocumento>> LeerFragmentosCercanosAsync(List<long> ids, List<long> idsPorPalabras,
        Vector vectorDeLaConsulta, CancellationToken cancellationToken, Dictionary<long, double>? distancias = null,
        Vector? vectorDeLaTraduccion = null)
    {
        await using var comando = _baseDatos.CreateCommand("""
            SELECT fragmentos.id, fragmentos.pagina, fragmentos.texto, documentos.nombre,
                   LEAST(fragmentos.embedding <=> $2, fragmentos.embedding <=> $5),
                   fragmentos.contexto
            FROM vectorial.fragmentos AS fragmentos
            JOIN vectorial.documentos AS documentos ON documentos.id = fragmentos.documento_id
            WHERE fragmentos.id = ANY($1)
              AND (LEAST(fragmentos.embedding <=> $2, fragmentos.embedding <=> $5) <= $3 OR fragmentos.id = ANY($4))
            """);
        comando.Parameters.AddWithValue(ids.ToArray());
        comando.Parameters.AddWithValue(vectorDeLaConsulta);
        comando.Parameters.AddWithValue(DistanciaMaximaPermitida);
        comando.Parameters.AddWithValue(idsPorPalabras.ToArray());
        comando.Parameters.AddWithValue(vectorDeLaTraduccion ?? vectorDeLaConsulta);

        var fragmentos = new List<FuenteDocumento>();
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            var id = lector.GetInt64(0);
            var pagina = lector.GetInt32(1);
            var texto = lector.GetString(2);
            var documento = lector.GetString(3);
            fragmentos.Add(new FuenteDocumento(id, pagina, texto, documento)
            {
                SeccionEnCurso = CabeceraDeFragmentos.ObtenerSeccion(lector.GetString(5))
            });
            if (distancias is not null && !lector.IsDBNull(4))
            {
                distancias[id] = lector.GetDouble(4);
            }
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

    /// <summary>
    /// Decide si la mejor sección responde la consulta. Con el puntaje del reranker en 1 o más, sí. Entre 0 y 1 se
    /// acepta salvo que el fragmento esté lejos de la consulta por significado: así se descartan secciones que solo
    /// comparten palabras, como la garantía de John Deere ante "Mantenimiento de 600 horas" (0,39 a 0,488 de
    /// distancia). Sin distancia (fragmento sin embedding) se acepta. Cuando la consulta está en español y el
    /// manual en inglés el reranker da puntajes más bajos aunque la sección sea la correcta: entonces se acepta si
    /// el puntaje no es muy bajo y el fragmento está cerca. Calibrado con consultas reales contra los manuales:
    /// las que no tenían respuesta quedaron todas por debajo de -1,8 en el reranker o lejos por significado.
    /// Si la sección nombra el intervalo de horas pedido, alcanza con el puntaje mínimo: el número ya la respalda, y un
    /// título como "250 Hours of service" puede quedar lejos de "Mantenimiento de 250 horas" aunque sea la correcta.
    /// </summary>
    internal static bool EsSeccionConfiable(float puntajeDelReranker, double? distanciaDelFragmento,
        bool coincideElIntervalo = false)
    {
        if (coincideElIntervalo)
        {
            return puntajeDelReranker >= PuntajeMinimoEntreIdiomas;
        }

        if (puntajeDelReranker >= PuntajeQueNoNecesitaDistancia)
        {
            return true;
        }

        if (puntajeDelReranker >= 0)
        {
            return distanciaDelFragmento is null || distanciaDelFragmento <= DistanciaMaximaPermitida;
        }

        return puntajeDelReranker >= PuntajeMinimoEntreIdiomas
            && distanciaDelFragmento is not null && distanciaDelFragmento <= DistanciaMaximaEntreIdiomas;
    }

    private List<SeccionDocumento> OrdenarPorRelevancia(string consulta, string? traduccion,
        List<SeccionDocumento> seccionesCandidatas, Dictionary<long, double> distancias)
    {
        var intervalosPedidos = IntervaloDeHoras.Extraer(consulta);
        var secciones = IntervaloDeHoras.QuitarOtrosIntervalos(seccionesCandidatas, intervalosPedidos);
        if (secciones.Count == 0)
        {
            return new List<SeccionDocumento>();
        }

        var textosParaOrdenar = new List<FuenteDocumento>();
        for (var posicion = 0; posicion < secciones.Count; posicion++)
        {
            var seccion = secciones[posicion];
            var textoDeLaSeccion = seccion.Titulo + ". " + seccion.Contexto + " " + seccion.Cuerpo;
            textosParaOrdenar.Add(new FuenteDocumento(posicion, seccion.Pagina, textoDeLaSeccion, seccion.Documento));
        }

        // Primero las secciones que nombran el intervalo pedido; dentro de cada grupo se respeta el orden del reranker.
        var resultados = PuntuarConLasDosConsultas(consulta, traduccion, textosParaOrdenar)
            .OrderByDescending(resultado =>
                IntervaloDeHoras.Coincide(secciones[(int)resultado.Fuente.Id], intervalosPedidos))
            .ToList();
        var relevantes = new List<SeccionDocumento>();
        if (resultados.Count == 0)
        {
            return relevantes;
        }

        // Entre idiomas, la sección con más puntaje puede ser una que solo coincidía por una palabra suelta y está lejos
        // por significado: se toma la primera, en el orden del reranker, que sea confiable.
        var primeraConfiable = -1;
        for (var posicion = 0; posicion < resultados.Count; posicion++)
        {
            var seccion = secciones[(int)resultados[posicion].Fuente.Id];
            double? distancia = distancias.TryGetValue(seccion.FragmentoId, out var valor) ? valor : null;
            if (EsSeccionConfiable(resultados[posicion].Puntaje, distancia,
                    IntervaloDeHoras.Coincide(seccion, intervalosPedidos)))
            {
                primeraConfiable = posicion;
                break;
            }
        }

        if (primeraConfiable == -1)
        {
            return relevantes;
        }

        var puntajeMinimo = resultados[primeraConfiable].Puntaje - MargenDePuntajeParaSeccionesRelevantes;
        for (var posicion = primeraConfiable; posicion < resultados.Count; posicion++)
        {
            var resultado = resultados[posicion];
            if (relevantes.Count == MaximoDeSeccionesRelevantes)
            {
                break;
            }

            // No se corta en el primero bajo: al ir primero las que nombran el intervalo, los puntajes no quedan en orden.
            if (resultado.Puntaje < puntajeMinimo)
            {
                continue;
            }

            var posicionDeLaSeccion = (int)resultado.Fuente.Id;
            relevantes.Add(secciones[posicionDeLaSeccion]);
        }

        return relevantes;
    }

    /// <summary>
    /// La consulta original puntúa todas las secciones; la traducción, solo las escritas en inglés. Cada sección se
    /// queda con su mejor puntaje. La traducción va sin el pedido que acompaña a la original: con él, una sección larga
    /// que apenas nombra la tarea ("500 hours of service") le ganaba a la que la explica ("Air intake system").
    /// </summary>
    private List<FuentePuntuada> PuntuarConLasDosConsultas(string consulta, string? traduccion,
        List<FuenteDocumento> textosParaOrdenar)
    {
        var resultados = _reranker.Ordenar(PedidoParaElReranker + consulta, textosParaOrdenar);
        if (traduccion is null)
        {
            return resultados;
        }

        var textosEnIngles = textosParaOrdenar.Where(texto => TraductorDeConsultas.EstaEnIngles(texto.Texto)).ToList();
        if (textosEnIngles.Count == 0)
        {
            return resultados;
        }

        var traducidos = _reranker.Ordenar(traduccion, textosEnIngles);
        return resultados.Concat(traducidos)
            .GroupBy(resultado => resultado.Fuente.Id)
            .Select(grupo => grupo.MaxBy(resultado => resultado.Puntaje)!)
            .OrderByDescending(resultado => resultado.Puntaje)
            .ToList();
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
        return SeccionesCandidatas.CompletarConFragmentoSiguiente(seccion, seccionesSiguientes);
    }

    internal static string CrearBusquedaTextual(string consulta)
    {
        var palabras = new List<string>();
        foreach (Match coincidencia in PatronPalabraDeBusqueda.Matches(consulta.ToLowerInvariant()))
        {
            var palabra = coincidencia.Value;
            if (palabra.Length >= 3 && !PalabrasSinValorEnIngles.Contains(palabra) && !palabras.Contains(palabra))
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
