using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.OrdenesTrabajo;

public class SolicitudSugerenciaDto
{
    public const int LargoMaximoDeLaDescripcion = 2000;

    /// <summary>Tarea a consultar. Se acota porque cada pedido genera embeddings y pasa por el modelo de IA.</summary>
    [MaxLength(LargoMaximoDeLaDescripcion, ErrorMessage = "La descripción no puede superar los 2000 caracteres.")]
    public string Descripcion { get; init; } = "";

    /// <summary>Número de la opción pedida; «otra sugerencia» incrementa este valor.</summary>
    [Range(1, 1000, ErrorMessage = "El número de sugerencia debe estar entre 1 y 1000.")]
    public int Numero { get; init; } = 1;

    public Guid? Sesion { get; init; }
}
