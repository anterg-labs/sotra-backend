using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.Repuestos;

public class RepuestoCrearDto
{
    [Required, MaxLength(50)]
    public required string Codigo { get; init; }

    [Required, MaxLength(120)]
    public required string Nombre { get; init; }

    public string? Descripcion { get; init; }

    [MaxLength(100)]
    public string? Marca { get; init; }

    [MaxLength(120)]
    public string? Ubicacion { get; init; }

    [Range(0, int.MaxValue)]
    public int Cantidad { get; init; }

    [Range(0, int.MaxValue)]
    public int CantidadMinima { get; init; }
}
