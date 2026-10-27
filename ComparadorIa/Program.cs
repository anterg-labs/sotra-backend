using System.Diagnostics;
using Api.Servicios;
using ComparadorIa;

var opciones = LeerOpciones(args);
if (opciones is null)
{
    Console.WriteLine("""
        Uso: dotnet run -- --corpus <carpeta con los PDF> [opciones]

          --casos <archivo>          Casos de evaluación (por defecto: casos.json)
          --proveedores <archivo>    Proveedores a comparar (por defecto: proveedores.json)
          --repeticiones <n>         Pedidos por caso y proveedor (por defecto: 3)
          --tiempo-maximo <seg>      Segundos antes de dar un pedido por fallido (por defecto: 30)
          --salida <carpeta>         Dónde guardar el informe (por defecto: resultados)

        Las claves se leen de las variables de entorno indicadas en proveedores.json;
        los proveedores sin clave se omiten.
        """);
    return 1;
}

var casos = await Configuracion.LeerCasosAsync(opciones.RutaCasos);
var configuraciones = await Configuracion.LeerProveedoresAsync(opciones.RutaProveedores);
var secciones = CorpusDeEvaluacion.LeerSecciones(opciones.RutaCorpus);
Console.WriteLine("Corpus: " + secciones.Count + " secciones en " + opciones.RutaCorpus);

var proveedores = new List<IProveedorDeTexto>();
foreach (var configuracion in configuraciones)
{
    if (!configuracion.Habilitado)
    {
        continue;
    }

    string? clave = null;
    if (!string.IsNullOrWhiteSpace(configuracion.VariableClave))
    {
        clave = Environment.GetEnvironmentVariable(configuracion.VariableClave);
        if (string.IsNullOrWhiteSpace(clave))
        {
            Console.WriteLine("Se omite " + configuracion.Nombre + ": falta la variable " + configuracion.VariableClave + ".");
            continue;
        }
    }

    proveedores.Add(FabricaDeProveedores.Crear(configuracion, clave));
}

if (proveedores.Count == 0)
{
    Console.WriteLine("No hay proveedores con clave: solo se muestran las secciones elegidas y el resumen sin IA.");
}

var resultados = new List<ResultadoDelCaso>();
foreach (var caso in casos)
{
    var resultado = new ResultadoDelCaso(caso);
    resultados.Add(resultado);

    var seccion = CorpusDeEvaluacion.ElegirSeccion(caso, secciones);
    if (seccion is null)
    {
        Console.WriteLine("\n[" + caso.Consulta + "] sin sección del corpus que coincida; se saltea.");
        continue;
    }

    // Igual que ServicioSugerencias: las advertencias se muestran aparte y no se resumen.
    var textoParaResumir = AdvertenciasDelManual.Quitar(seccion.Cuerpo);
    if (textoParaResumir.Length < SeccionesCandidatas.LargoMinimoDeSeccion)
    {
        textoParaResumir = seccion.Cuerpo;
    }

    resultado.Seccion = seccion;
    resultado.ResumenSinIa = TextoDelManual.CrearResumenExtractivo(textoParaResumir);
    var mensaje = RedaccionDeSugerencia.CrearMensajeParaElModelo(caso.Consulta, seccion, textoParaResumir);

    Console.WriteLine("\n[" + caso.Consulta + "] → " + seccion.Documento + ", pág. " + seccion.Pagina
        + ", \"" + TextoDelManual.CrearTituloLegible(seccion.Titulo) + "\"");

    foreach (var proveedor in proveedores)
    {
        for (var repeticion = 1; repeticion <= opciones.Repeticiones; repeticion++)
        {
            var intento = await GenerarAsync(proveedor, mensaje, caso, seccion, opciones.SegundosMaximos);
            resultado.Intentos.Add(intento);
            Console.WriteLine("  " + proveedor.Configuracion.Nombre + " #" + repeticion + ": " + DescribirIntento(intento));
        }
    }
}

var rutaDelInforme = Informe.Escribir(opciones.CarpetaDeSalida, resultados, proveedores, opciones.Repeticiones);
Console.WriteLine("\nInforme: " + rutaDelInforme);
return 0;

static async Task<IntentoDeGeneracion> GenerarAsync(IProveedorDeTexto proveedor, string mensaje,
    CasoDeEvaluacion caso, SeccionDocumento seccion, int segundosMaximos)
{
    var intento = new IntentoDeGeneracion(proveedor.Configuracion.Nombre);
    using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(segundosMaximos));
    var cronometro = Stopwatch.StartNew();
    try
    {
        var generado = await proveedor.GenerarAsync(RedaccionDeSugerencia.Instrucciones, mensaje, limite.Token);
        intento.Texto = generado.Texto;
        intento.TokensEntrada = generado.TokensEntrada;
        intento.TokensSalida = generado.TokensSalida;
        intento.Exito = true;
        var respuesta = RedaccionDeSugerencia.SepararTitulo(generado.Texto);
        intento.Confiable = RedaccionDeSugerencia.EsResumenConfiable(respuesta.Resumen, caso.Consulta, seccion,
            respuesta.Titulo);
        foreach (var valor in caso.ValoresEsperados)
        {
            if (!CorpusDeEvaluacion.ContieneValor(generado.Texto, valor))
            {
                intento.ValoresFaltantes.Add(valor);
            }
        }
    }
    catch (OperationCanceledException) when (limite.IsCancellationRequested)
    {
        intento.Error = "sin respuesta en " + segundosMaximos + " s";
    }
    catch (Exception excepcion)
    {
        intento.Error = excepcion.GetType().Name + ": " + excepcion.Message;
    }

    intento.Segundos = cronometro.Elapsed.TotalSeconds;
    return intento;
}

static string DescribirIntento(IntentoDeGeneracion intento)
{
    if (!intento.Exito)
    {
        return "ERROR (" + intento.Error + ")";
    }

    var descripcion = intento.Segundos.ToString("0.00") + " s, " + (intento.Confiable ? "confiable" : "DESCARTADO");
    if (intento.ValoresFaltantes.Count > 0)
    {
        descripcion += ", faltan: " + string.Join(", ", intento.ValoresFaltantes);
    }

    return descripcion;
}

static Opciones? LeerOpciones(string[] argumentos)
{
    var opciones = new Opciones();
    for (var indice = 0; indice < argumentos.Length - 1; indice += 2)
    {
        var valor = argumentos[indice + 1];
        switch (argumentos[indice])
        {
            case "--corpus": opciones.RutaCorpus = valor; break;
            case "--casos": opciones.RutaCasos = valor; break;
            case "--proveedores": opciones.RutaProveedores = valor; break;
            case "--salida": opciones.CarpetaDeSalida = valor; break;
            case "--repeticiones": opciones.Repeticiones = int.Parse(valor); break;
            case "--tiempo-maximo": opciones.SegundosMaximos = int.Parse(valor); break;
            default: return null;
        }
    }

    if (argumentos.Length % 2 != 0 || !Directory.Exists(opciones.RutaCorpus) || opciones.Repeticiones < 1)
    {
        return null;
    }

    return opciones;
}

class Opciones
{
    private const int RepeticionesPorDefecto = 3;
    private const int SegundosMaximosPorPedidoPorDefecto = 30;

    public string RutaCorpus { get; set; } = "";
    public string RutaCasos { get; set; } = Path.Combine(AppContext.BaseDirectory, "casos.json");
    public string RutaProveedores { get; set; } = Path.Combine(AppContext.BaseDirectory, "proveedores.json");
    public string CarpetaDeSalida { get; set; } = BuscarCarpetaDeResultados();
    public int Repeticiones { get; set; } = RepeticionesPorDefecto;
    public int SegundosMaximos { get; set; } = SegundosMaximosPorPedidoPorDefecto;

    /// <summary>
    /// Guarda los informes en ComparadorIa/resultados, que está en el .gitignore, sin importar desde qué carpeta se
    /// ejecute: se sube desde bin hasta encontrar el proyecto.
    /// </summary>
    private static string BuscarCarpetaDeResultados()
    {
        for (var carpeta = new DirectoryInfo(AppContext.BaseDirectory); carpeta is not null; carpeta = carpeta.Parent)
        {
            if (File.Exists(Path.Combine(carpeta.FullName, "ComparadorIa.csproj")))
            {
                return Path.Combine(carpeta.FullName, "resultados");
            }
        }

        return Path.Combine(AppContext.BaseDirectory, "resultados");
    }
}
