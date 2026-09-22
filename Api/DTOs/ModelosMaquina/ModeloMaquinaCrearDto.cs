using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.ModelosMaquina;

public class ModeloMaquinaCrearDto
{
    [Required, MaxLength(60)]
    public required string Nombre { get; init; }

    [Range(1, long.MaxValue)]
    public long TipoMaquinaId { get; init; }

    public string? Descripcion { get; init; }
}
