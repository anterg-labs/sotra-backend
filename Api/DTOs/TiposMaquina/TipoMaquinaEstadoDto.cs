using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.TiposMaquina;

public class TipoMaquinaEstadoDto
{
    /// <summary>Estado del registro (`activo` o `inactivo`) o, en órdenes, valor del estado vigente del catálogo.</summary>
    [Required]
    public required string Estado { get; init; }
}
