using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.Activos;

public class ActivoEstadoDto
{
    /// <summary>Estado del registro (`activo` o `inactivo`) o, en órdenes, valor del estado vigente del catálogo.</summary>
    [Required]
    public required string Estado { get; init; }
}
