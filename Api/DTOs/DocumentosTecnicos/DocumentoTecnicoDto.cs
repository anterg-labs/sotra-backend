namespace Api.DTOs.DocumentosTecnicos;

public class DocumentoTecnicoDto
{
    public long Id { get; init; }
    public required string NombreArchivo { get; init; }
    public required string Titulo { get; init; }
    public required string Tipo { get; init; }
    public string? Descripcion { get; init; }
    public bool Vigente { get; init; } = true;
    public long? ActivoId { get; init; }
    public string? NombreActivo { get; init; }
    public long? ModeloMaquinaId { get; init; }
    public string? NombreModelo { get; init; }
    public bool UsaSugerenciasIA { get; init; }
    public int CantidadFragmentos { get; init; }
    public DateTime CreadoEn { get; init; }

    /// <summary><c>pendiente</c>, <c>procesando</c>, <c>listo</c> o <c>error</c>. Sin IA, siempre <c>listo</c>.</summary>
    public required string EstadoProcesamiento { get; init; }

    /// <summary>Código del último error del procesamiento; <c>null</c> si no hubo.</summary>
    public string? CodigoErrorProcesamiento { get; init; }

    public string? MensajeErrorProcesamiento { get; init; }
}
