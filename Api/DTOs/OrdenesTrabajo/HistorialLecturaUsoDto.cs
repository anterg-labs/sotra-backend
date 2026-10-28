namespace Api.DTOs.OrdenesTrabajo;

/// <summary>Un cambio de la lectura de uso de una orden.</summary>
public class HistorialLecturaUsoDto
{
    /// <summary>Lectura anterior; vacía la primera vez.</summary>
    public decimal? LecturaAnterior { get; init; }
    /// <summary>Lectura nueva.</summary>
    public decimal? LecturaNueva { get; init; }
    /// <summary>Fecha y hora del cambio (ISO 8601, UTC).</summary>
    public DateTime FechaHora { get; init; }
    /// <summary>Identificador del usuario que hizo el cambio, si se conoce.</summary>
    public long? UsuarioId { get; init; }
    /// <summary>Nombre del usuario que hizo el cambio.</summary>
    public string? NombreUsuario { get; init; }
}
