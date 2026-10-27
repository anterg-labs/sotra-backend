namespace Api.DTOs.ModelosMaquina;

public class ModeloMaquinaDto
{
    /// <summary>Identificador del registro.</summary>
    public long Id { get; init; }
    /// <summary>Nombre.</summary>
    public required string Nombre { get; init; }
    /// <summary>Identificador del tipo de máquina.</summary>
    public long TipoMaquinaId { get; init; }
    /// <summary>Nombre del tipo de máquina.</summary>
    public string? NombreTipoMaquina { get; init; }
    /// <summary>Descripción.</summary>
    public string? Descripcion { get; init; }
    /// <summary>Estado del registro (`activo` o `inactivo`) o, en órdenes, valor del estado vigente del catálogo.</summary>
    public required string Estado { get; init; }
}
