using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.Terminos;

public class TerminosDto
{
    /// <summary>Identificador del registro.</summary>
    public long Id { get; init; }
    /// <summary>Versión del registro, para el control de cambios concurrentes.</summary>
    public int Version { get; init; }
    /// <summary>Texto.</summary>
    public required string Texto { get; init; }


    /// <summary>Indica si el documento está vigente.</summary>
    public bool Vigente { get; init; }
    /// <summary>Fecha y hora de creación (ISO 8601, UTC).</summary>
    public DateTime CreadoEn { get; init; }
}

public class TerminosPublicarDto
{
    /// <summary>Texto.</summary>
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

    /// <summary>Indica si la persona acepta los términos.</summary>
    [Required(ErrorMessage = "Hay que marcar que se leyeron y aceptaron los términos.")]
    public bool? Acepta { get; init; }
}

public class EstadoTerminosDto
{
    /// <summary>Indica si el documento está vigente.</summary>
    public TerminosDto? Vigente { get; init; }
    /// <summary>Indica si falta aceptar.</summary>
    public bool Pendiente { get; init; }
    /// <summary>Versión de los términos aceptada.</summary>
    public int? VersionAceptada { get; init; }
    /// <summary>Fecha y hora de aceptación.</summary>
    public DateTime? AceptadoEn { get; init; }
}

