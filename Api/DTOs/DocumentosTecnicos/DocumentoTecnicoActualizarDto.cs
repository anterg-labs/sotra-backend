using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace Api.DTOs.DocumentosTecnicos;

/// <summary>Cuerpo de <c>PUT /documentos-tecnicos/{id}</c>: reemplaza los datos generales sin volver a subir el archivo.</summary>
public class DocumentoTecnicoActualizarDto
{
    [JsonRequired]
    public string? Titulo { get; init; }
    [JsonRequired]
    [Required]
    public string Tipo { get; init; } = TiposDeDocumento.ManualTecnico;
    [JsonRequired]
    public string? Descripcion { get; init; }
    [JsonRequired]
    public long? ActivoId { get; init; }
    [JsonRequired]
    public long? ModeloMaquinaId { get; init; }
}

/// <summary>Cuerpo de <c>PATCH /documentos-tecnicos/{id}/vigencia</c>.</summary>
public class DocumentoTecnicoVigenciaDto
{
    [JsonRequired]
    public bool Vigente { get; init; }
}
