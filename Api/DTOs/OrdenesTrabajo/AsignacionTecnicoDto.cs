using System.Text.Json.Serialization;
namespace Api.DTOs.OrdenesTrabajo;

public class AsignacionTecnicoDto
{
    [JsonRequired]
    public long TecnicoId { get; init; }
}
