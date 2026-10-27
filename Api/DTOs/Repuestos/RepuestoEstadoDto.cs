namespace Api.DTOs.Repuestos;

public class RepuestoEstadoDto
{
    /// <summary>Estado del registro (`activo` o `inactivo`) o, en órdenes, valor del estado vigente del catálogo.</summary>
    public required string Estado { get; init; }
}
