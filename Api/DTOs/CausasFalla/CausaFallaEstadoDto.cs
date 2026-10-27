using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.CausasFalla;

public class CausaFallaEstadoDto
{
    /// <summary>Estado del registro: `activo` o `inactivo`.</summary>
    [Required]
    public required string Estado { get; init; }
}
