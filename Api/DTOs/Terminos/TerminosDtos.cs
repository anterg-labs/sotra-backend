using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.Terminos;

public class TerminosDto
{
    public long Id { get; init; }
    public int Version { get; init; }
    public required string Texto { get; init; }


    public bool Vigente { get; init; }
    public DateTime CreadoEn { get; init; }
}

public class TerminosPublicarDto
{
    [Required(ErrorMessage = "El texto es obligatorio."), StringLength(20000, MinimumLength = 20,
        ErrorMessage = "El texto debe tener entre 20 y 20000 caracteres.")]
    public required string Texto { get; init; }


}

public class AceptacionTerminosDto
{
    /// <summary>Persona que acepta. Temporal hasta que se tome del token (Keycloak).</summary>
    [Range(1, long.MaxValue, ErrorMessage = "Indicá la persona usuaria.")]
    public long UsuarioId { get; init; }

    /// <summary>Versión aceptada: debe ser la vigente.</summary>
    [Range(1, long.MaxValue, ErrorMessage = "Indicá los términos que se aceptan.")]
    public long TerminosId { get; init; }

    /// <summary>Marcador del contenido leído; impide aceptar el texto anterior a una modificación.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Indicá el contenido de los términos que se leyó.")]
    public int Version { get; init; }

    [Required(ErrorMessage = "Hay que marcar que se leyeron y aceptaron los términos.")]
    public bool? Acepta { get; init; }
}

public class EstadoTerminosDto
{
    public TerminosDto? Vigente { get; init; }
    public bool Pendiente { get; init; }
    public int? VersionAceptada { get; init; }
    public DateTime? AceptadoEn { get; init; }
}

