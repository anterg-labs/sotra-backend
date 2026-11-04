namespace Api.Servicios;

/// <summary>
/// Arma la cabecera de contexto de cada fragmento: el documento, la máquina y, si el fragmento empieza a mitad de una
/// sección, el título de esa sección. Un fragmento sin título propio ("Revise el nivel...") se encuentra así por el
/// manual, la máquina y la sección a la que pertenece. El texto del fragmento no cambia: la cabecera se guarda aparte.
/// </summary>
public static class CabeceraDeFragmentos
{
    private const string ExtensionPdf = ".pdf";

    // Un "título" que aparece en tantos fragmentos es un encabezado o pie de página ("SANY LATIN AMERICA"), no una
    // sección del manual.
    private const int FragmentosConElMismoTituloDeUnEncabezado = 5;

    /// <summary>
    /// <c>Completa</c> (documento, máquina y sección) va en la búsqueda por palabras. <c>DeSeccion</c> (solo la
    /// sección) va en el embedding: si el embedding llevara el documento y la máquina, una consulta que nombra la
    /// máquina quedaría cerca de todos los fragmentos de su manual, y la distancia dejaría de servir para descartar
    /// las secciones que no responden.
    /// </summary>
    public static List<CabeceraDeFragmento> Armar(string documento, string? maquina, List<FragmentoDocumento> fragmentos)
    {
        var inicio = "Documento: " + QuitarExtension(documento) + ".";
        if (!string.IsNullOrWhiteSpace(maquina))
        {
            inicio += " Máquina: " + maquina.Trim() + ".";
        }

        var seccionesPorFragmento = new List<List<SeccionDocumento>>();
        var fragmentosPorTitulo = new Dictionary<string, int>();
        foreach (var fragmento in fragmentos)
        {
            var secciones = TextoDelManual.DividirEnSecciones(
                new FuenteDocumento(0, fragmento.Pagina, fragmento.Texto, documento));
            seccionesPorFragmento.Add(secciones);

            var titulosDelFragmento = new HashSet<string>();
            foreach (var seccion in secciones)
            {
                if (seccion.Titulo.Length > 0)
                {
                    titulosDelFragmento.Add(seccion.Titulo.ToLowerInvariant());
                }
            }

            foreach (var titulo in titulosDelFragmento)
            {
                fragmentosPorTitulo[titulo] = fragmentosPorTitulo.GetValueOrDefault(titulo) + 1;
            }
        }

        bool EsTituloDeSeccion(string titulo) => titulo.Length > 0
            && fragmentosPorTitulo[titulo.ToLowerInvariant()] < FragmentosConElMismoTituloDeUnEncabezado;

        var cabeceras = new List<CabeceraDeFragmento>();
        var seccionEnCurso = "";
        foreach (var secciones in seccionesPorFragmento)
        {
            var deSeccion = "";
            var empiezaSinTitulo = secciones.Count == 0 || !EsTituloDeSeccion(secciones[0].Titulo);
            if (empiezaSinTitulo && seccionEnCurso.Length > 0)
            {
                deSeccion = "Sección: " + TextoDelManual.CrearTituloLegible(seccionEnCurso) + ".";
            }

            var completa = deSeccion.Length > 0 ? inicio + " " + deSeccion : inicio;
            cabeceras.Add(new CabeceraDeFragmento(completa, deSeccion));

            for (var indice = secciones.Count - 1; indice >= 0; indice--)
            {
                var titulo = secciones[indice].Titulo;
                if (EsTituloDeSeccion(titulo))
                {
                    seccionEnCurso = titulo;
                    break;
                }
            }
        }

        return cabeceras;
    }

    private static string QuitarExtension(string documento)
    {
        var nombre = documento.Trim();
        if (nombre.EndsWith(ExtensionPdf, StringComparison.OrdinalIgnoreCase))
        {
            nombre = nombre.Substring(0, nombre.Length - ExtensionPdf.Length).Trim();
        }

        return nombre;
    }
}

public record CabeceraDeFragmento(string Completa, string DeSeccion);
