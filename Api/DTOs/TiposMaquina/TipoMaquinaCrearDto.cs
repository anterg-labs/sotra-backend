using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.TiposMaquina;

public class TipoMaquinaCrearDto
{
    /// <summary>Nombre.</summary>
    [Required(ErrorMessage = "El campo Nombre es obligatorio."), MaxLength(60, ErrorMessage = "El campo Nombre no puede superar los 60 caracteres.")]
    public required string Nombre { get; init; }

    /// <summary>Costo por hora de parada de este tipo de máquina, opcional. Si no se envía, no cambia lo guardado.</summary>
    [Range(0.0, 999999999.0, ErrorMessage = "El costo por hora de parada no puede ser negativo.")]
    public decimal? CostoHoraParada { get; init; }
}
