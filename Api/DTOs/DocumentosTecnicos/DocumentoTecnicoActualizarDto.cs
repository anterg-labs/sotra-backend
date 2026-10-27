using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace Api.DTOs.DocumentosTecnicos;

/// <summary>Cuerpo de <c>PUT /documentos-tecnicos/{id}</c>: reemplaza los datos generales sin volver a subir el archivo.</summary>
public class DocumentoTecnicoActualizarDto
{
    /// <summary>Título.</summary>
    [JsonRequired]
    public string? Titulo { get; init; }
    /// <summary>Tipo; los valores posibles dependen del recurso (por ejemplo `preventivo` o `correctivo`).</summary>
    [JsonRequired]
    [Required]
    public string Tipo { get; init; } = TiposDeDocumento.ManualTecnico;
    /// <summary>Descripción.</summary>
    [JsonRequired]
    public string? Descripcion { get; init; }
    /// <summary>Identificador del activo.</summary>
    [JsonRequired]
    public long? ActivoId { get; init; }
    /// <summary>Identificador del modelo de máquina.</summary>
    [JsonRequired]
    public long? ModeloMaquinaId { get; init; }
}

/// <summary>Cuerpo de <c>PATCH /documentos-tecnicos/{id}/vigencia</c>.</summary>
public class DocumentoTecnicoVigenciaDto
{
    /// <summary>Indica si el documento está vigente.</summary>
    [JsonRequired]
    public bool Vigente { get; init; }
}
