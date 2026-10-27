using System.Threading.Channels;

namespace Api.Servicios;

/// <summary>Estado del procesamiento de un documento técnico para las sugerencias de IA.</summary>
public static class EstadosDeProcesamiento
{
    /// <summary>El PDF está guardado y espera su turno en la cola.</summary>
    public const string Pendiente = "pendiente";

    /// <summary>Se están extrayendo los fragmentos y calculando los embeddings.</summary>
    public const string Procesando = "procesando";

    /// <summary>Los fragmentos están guardados; sin IA, el documento queda así desde el alta.</summary>
    public const string Listo = "listo";

    /// <summary>El procesamiento falló; el código y el mensaje dicen por qué.</summary>
    public const string Error = "error";
}

public static class ErroresDeProcesamiento
{
    public const string PdfInvalido = "PDF_INVALIDO";
    public const string PdfSinTexto = "PDF_SIN_TEXTO";
    public const string ServicioIaNoDisponible = "SERVICIO_IA_NO_DISPONIBLE";
    public const string ProcesamientoFallido = "PROCESAMIENTO_FALLIDO";

    public static string Mensaje(string codigo) => codigo switch
    {
        PdfInvalido => "No se pudo leer el PDF.",
        PdfSinTexto => "El PDF no tiene texto que se pueda leer; puede ser un documento escaneado.",
        ServicioIaNoDisponible => "El servicio de IA no estaba disponible. Volvé a procesar el documento más tarde.",
        _ => "No se pudo procesar el documento. Volvé a intentarlo más tarde.",
    };
}

/// <summary>
/// Ids de los documentos técnicos que esperan ser procesados. La API responde la subida en cuanto guarda el PDF y
/// <see cref="Api.Infraestructura.ProcesadorDeDocumentosTecnicos"/> los toma de a uno, porque los embeddings ocupan
/// la CPU durante minutos.
/// </summary>
public sealed class ColaDeDocumentosTecnicos
{
    private readonly Channel<long> _canal = Channel.CreateUnbounded<long>(new UnboundedChannelOptions
    {
        SingleReader = true,
    });

    public void Encolar(long documentoId)
    {
        _canal.Writer.TryWrite(documentoId);
    }

    public IAsyncEnumerable<long> LeerAsync(CancellationToken cancellationToken)
    {
        return _canal.Reader.ReadAllAsync(cancellationToken);
    }
}
