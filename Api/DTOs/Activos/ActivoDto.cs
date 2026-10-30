namespace Api.DTOs.Activos;

public class ActivoDto
{
    /// <summary>Identificador del registro.</summary>
    public long Id { get; init; }
    /// <summary>Identificador del cliente.</summary>
    public long ClienteId { get; init; }
    /// <summary>Nombre del cliente.</summary>
    public string? NombreCliente { get; init; }
    /// <summary>Número de serie.</summary>
    public required string NumSerie { get; init; }
    /// <summary>Descripción.</summary>
    public string? Descripcion { get; init; }
    /// <summary>Identificador del modelo de máquina.</summary>
    public long ModeloMaquinaId { get; init; }
    /// <summary>Nombre del modelo de máquina.</summary>
    public string? NombreModeloMaquina { get; init; }
    /// <summary>Identificador del tipo de máquina del modelo.</summary>
    public long? TipoMaquinaId { get; init; }
    /// <summary>Nombre del tipo de máquina del modelo.</summary>
    public string? NombreTipoMaquina { get; init; }
    /// <summary>Año de fabricación.</summary>
    public short? AnioFabricacion { get; init; }
    /// <summary>Nombre.</summary>
    public string? Nombre { get; init; }
    /// <summary>Ubicación o depósito.</summary>
    public string? Ubicacion { get; init; }
    /// <summary>Estado del registro (`activo` o `inactivo`) o, en órdenes, valor del estado vigente del catálogo.</summary>
    public required string Estado { get; init; }

    /// <summary>Cómo se mide el uso del activo: `horas` (horómetro) o `kilometros` (odómetro).</summary>
    public string UnidadUso { get; init; } = "horas";
}
