namespace Api.DTOs.TiposMaquina;

public class TipoMaquinaDto
{
    /// <summary>Identificador del registro.</summary>
    public long Id { get; init; }
    /// <summary>Nombre.</summary>
    public required string Nombre { get; init; }
    /// <summary>Estado del registro (`activo` o `inactivo`) o, en órdenes, valor del estado vigente del catálogo.</summary>
    public required string Estado { get; init; }
}
