using System.Text.Json.Serialization;
namespace Api.DTOs.OrdenesTrabajo;

public class AsignacionTecnicoDto
{
    /// <summary>Identificador del técnico.</summary>
    [JsonRequired]
    public long TecnicoId { get; init; }
}
