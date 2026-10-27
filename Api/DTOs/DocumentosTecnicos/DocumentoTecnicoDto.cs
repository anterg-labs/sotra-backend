namespace Api.DTOs.DocumentosTecnicos;

public class DocumentoTecnicoDto
{
    /// <summary>Identificador del registro.</summary>
    public long Id { get; init; }
    /// <summary>Nombre del archivo.</summary>
    public required string NombreArchivo { get; init; }
    /// <summary>Título.</summary>
    public required string Titulo { get; init; }
    /// <summary>Tipo; los valores posibles dependen del recurso (por ejemplo `preventivo` o `correctivo`).</summary>
    public required string Tipo { get; init; }
    /// <summary>Descripción.</summary>
    public string? Descripcion { get; init; }
    /// <summary>Indica si el documento está vigente.</summary>
    public bool Vigente { get; init; } = true;
    /// <summary>Identificador del activo.</summary>
    public long? ActivoId { get; init; }
    /// <summary>Nombre del activo (o su número de serie si no tiene nombre).</summary>
    public string? NombreActivo { get; init; }
    /// <summary>Identificador del modelo de máquina.</summary>
    public long? ModeloMaquinaId { get; init; }
    /// <summary>Nombre del modelo.</summary>
    public string? NombreModelo { get; init; }
    /// <summary>Indica si el documento se usa para las sugerencias de IA.</summary>
    public bool UsaSugerenciasIA { get; init; }
    /// <summary>Cantidad de fragmentos indexados para la consulta asistida.</summary>
    public int CantidadFragmentos { get; init; }
    /// <summary>Fecha y hora de creación (ISO 8601, UTC).</summary>
    public DateTime CreadoEn { get; init; }
}
