using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Api.Servicios;

public static class DocumentoPdf
{
    private const int LargoMaximoDeFragmento = 1400;
    private const int LargoMinimoDeFragmento = 700;
    private const int CaracteresRepetidosEntreFragmentos = 200;
    private const string GuionInvisible = "\u00ad";

    private static readonly Regex PatronCaracteresDeControl = new("[\\x00-\\x08\\x0B\\x0C\\x0E-\\x1F]");
    private static readonly Regex PatronEspacios = new("\\s+");

    public static List<FragmentoDocumento> Extraer(byte[] archivo)
    {
        var fragmentos = new List<FragmentoDocumento>();
        using var memoria = new MemoryStream(archivo);
        using var documento = PdfDocument.Open(memoria);

        foreach (var pagina in documento.GetPages())
        {
            var texto = Normalizar(ContentOrderTextExtractor.GetText(pagina));
            foreach (var fragmento in Dividir(texto))
            {
                fragmentos.Add(new FragmentoDocumento(pagina.Number, fragmento));
            }
        }

        return fragmentos;
    }

    public static List<string> Dividir(string texto)
    {
        var fragmentos = new List<string>();
        var inicio = 0;
        while (inicio < texto.Length)
        {
            var fin = BuscarFinDelFragmento(texto, inicio);

            var fragmento = texto.Substring(inicio, fin - inicio).Trim();
            if (fragmento.Length > 0)
            {
                fragmentos.Add(fragmento);
            }

            var llegoAlFinalDelTexto = fin == texto.Length;
            if (llegoAlFinalDelTexto)
            {
                break;
            }

            inicio = BuscarInicioDelSiguienteFragmento(texto, inicio, fin);
        }

        return fragmentos;
    }

    private static int BuscarFinDelFragmento(string texto, int inicio)
    {
        var finMaximo = inicio + LargoMaximoDeFragmento;
        if (finMaximo >= texto.Length)
        {
            return texto.Length;
        }

        var ultimoEspacio = texto.LastIndexOf(' ', finMaximo - 1, finMaximo - inicio);
        var cortarEnElEspacioDejaUnFragmentoLargo = ultimoEspacio > inicio + LargoMinimoDeFragmento;
        if (cortarEnElEspacioDejaUnFragmentoLargo)
        {
            return ultimoEspacio;
        }

        return finMaximo;
    }

    private static int BuscarInicioDelSiguienteFragmento(string texto, int inicio, int fin)
    {
        var inicioConRepeticion = fin - CaracteresRepetidosEntreFragmentos;
        var siguienteInicio = inicioConRepeticion;
        if (siguienteInicio <= inicio)
        {
            siguienteInicio = inicio + 1;
        }

        var siguienteEspacio = texto.IndexOf(' ', siguienteInicio);
        var hayUnEspacioAntesDelFin = siguienteEspacio >= siguienteInicio && siguienteEspacio < fin;
        if (hayUnEspacioAntesDelFin)
        {
            return siguienteEspacio + 1;
        }

        return siguienteInicio;
    }

    public static string Normalizar(string texto)
    {
        var normalizado = PatronCaracteresDeControl.Replace(texto, "");
        normalizado = normalizado.Normalize(NormalizationForm.FormKC);
        normalizado = normalizado.Replace(GuionInvisible, "");
        return PatronEspacios.Replace(normalizado, " ").Trim();
    }
}

public class FragmentoDocumento
{
    public int Pagina { get; set; }
    public string Texto { get; set; }

    public FragmentoDocumento(int pagina, string texto)
    {
        Pagina = pagina;
        Texto = texto;
    }
}

public class FuenteDocumento
{
    public long Id { get; set; }
    public int Pagina { get; set; }
    public string Texto { get; set; }
    public string Documento { get; set; }

    public FuenteDocumento(long id, int pagina, string texto, string documento)
    {
        Id = id;
        Pagina = pagina;
        Texto = texto;
        Documento = documento;
    }
}

public class FuentePuntuada
{
    public FuenteDocumento Fuente { get; set; }
    public float Puntaje { get; set; }

    public FuentePuntuada(FuenteDocumento fuente, float puntaje)
    {
        Fuente = fuente;
        Puntaje = puntaje;
    }
}

public class SeccionDocumento
{
    public long FragmentoId { get; set; }
    public int Pagina { get; set; }
    public string Documento { get; set; }
    public string Titulo { get; set; }
    public string Contexto { get; set; }
    public string Cuerpo { get; set; }
    public bool LlegaAlFinal { get; set; }

    public SeccionDocumento(long fragmentoId, int pagina, string documento, string titulo, string contexto, string cuerpo, bool llegaAlFinal)
    {
        FragmentoId = fragmentoId;
        Pagina = pagina;
        Documento = documento;
        Titulo = titulo;
        Contexto = contexto;
        Cuerpo = cuerpo;
        LlegaAlFinal = llegaAlFinal;
    }
}

public class TituloEncontrado
{
    public int Inicio { get; set; }
    public int Fin { get; set; }
    public string Texto { get; set; }

    public TituloEncontrado(int inicio, int fin, string texto)
    {
        Inicio = inicio;
        Fin = fin;
        Texto = texto;
    }
}

public class ArchivoGuardado
{
    public byte[] Contenido { get; set; }
    public string NombreArchivo { get; set; }

    public ArchivoGuardado(byte[] contenido, string nombreArchivo)
    {
        Contenido = contenido;
        NombreArchivo = nombreArchivo;
    }
}
