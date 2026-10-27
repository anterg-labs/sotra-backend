namespace Api.DTOs.OrdenesTrabajo;

public class OrdenTrabajoDto
{
    /// <summary>Identificador del registro.</summary>
    public long Id { get; init; }
    /// <summary>Identificador del cliente.</summary>
    public long ClienteId { get; init; }
    /// <summary>Nombre del cliente.</summary>
    public required string NombreCliente { get; init; }
    /// <summary>Identificador del activo.</summary>
    public long ActivoId { get; init; }
    /// <summary>Nombre del activo (o su número de serie si no tiene nombre).</summary>
    public required string NombreActivo { get; init; }

    /// <summary>Quién creó la orden (el responsable); sin sesión es el primer jefe de mantenimiento activo.</summary>
    public long ResponsableId { get; init; }
    /// <summary>Nombre de quien hizo el cambio.</summary>
    public string? NombreResponsable { get; init; }
    /// <summary>Identificador del técnico.</summary>
    public long? TecnicoId { get; init; }
    /// <summary>Nombre del técnico.</summary>
    public string? NombreTecnico { get; init; }
    /// <summary>Tipo; los valores posibles dependen del recurso (por ejemplo `preventivo` o `correctivo`).</summary>
    public required string Tipo { get; init; }
    /// <summary>Prioridad: `alta`, `media` o `baja`.</summary>
    public required string Prioridad { get; init; }
    /// <summary>Descripción.</summary>
    public required string Descripcion { get; init; }
    /// <summary>Observaciones adicionales, opcionales.</summary>
    public string Observaciones { get; init; } = "";
    /// <summary>Fecha prevista de ejecución (`aaaa-mm-dd`).</summary>
    public required string FechaPrevistaEjecucion { get; init; }
    /// <summary>Estado del registro (`activo` o `inactivo`) o, en órdenes, valor del estado vigente del catálogo.</summary>
    public required string Estado { get; init; }
    /// <summary>Fecha y hora de creación (ISO 8601, UTC).</summary>
    public DateTime CreadoEn { get; init; }
    /// <summary>Fecha y hora de inicio de la ejecución.</summary>
    public DateTime? FechaInicio { get; init; }
    /// <summary>Fecha y hora de cierre.</summary>
    public DateTime? FechaCierre { get; init; }
    /// <summary>Sugerencias de IA aceptadas para la orden.</summary>
    public List<string> Sugerencias { get; init; } = new();
    /// <summary>Repuestos con sus cantidades.</summary>
    public List<RepuestoOrdenDto> Repuestos { get; init; } = new();
    /// <summary>Estados a los que puede pasar la orden desde el actual.</summary>
    public List<TransicionOrdenDto> Transiciones { get; init; } = new();
    /// <summary>Historial de cambios de estado.</summary>
    public List<HistorialEstadoDto> HistorialEstados { get; init; } = new();
    /// <summary>Historial de asignaciones de técnico.</summary>
    public List<HistorialTecnicoDto> HistorialTecnicos { get; init; } = new();
    /// <summary>Historial de cambios de repuestos.</summary>
    public List<HistorialRepuestoDto> HistorialRepuestos { get; init; } = new();

    /// <summary>Huella del contenido actual: cambia con cualquier modificación. Es el valor del encabezado <c>ETag</c>.</summary>
    public string Version { get; internal set; } = "";
}
