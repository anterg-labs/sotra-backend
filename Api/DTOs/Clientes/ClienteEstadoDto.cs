using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.Clientes;

public class ClienteEstadoDto
{
    /// <summary>Estado del registro (`activo` o `inactivo`) o, en órdenes, valor del estado vigente del catálogo.</summary>
    [Required]
    public required string Estado { get; init; }
}
