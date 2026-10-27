namespace Api.DTOs.CausasFalla;

/// <summary>Causa de falla del catálogo.</summary>
public class CausaFallaDto
{
    /// <summary>Identificador.</summary>
    public long Id { get; init; }

    /// <summary>Nombre de la causa.</summary>
    public required string Nombre { get; init; }

    /// <summary>Estado del registro: `activo` o `inactivo`.</summary>
    public required string Estado { get; init; }
}
