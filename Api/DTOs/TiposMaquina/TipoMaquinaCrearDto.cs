using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.TiposMaquina;

public class TipoMaquinaCrearDto
{
    [Required, MaxLength(60)]
    public required string Nombre { get; init; }
}
