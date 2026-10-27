using SOTRA_BACKEND.Operaciones;

namespace Api.DTOs.DocumentosTecnicos;

/// <summary>Catálogo cerrado de tipos de documento (G1 de la sección 18 del plan).</summary>
public static class TiposDeDocumento
{
    public const string ManualTecnico = "manual_tecnico";
    public const int LargoMaximoTitulo = 200;
    public const int LargoMaximoDescripcion = 1000;

    public static readonly IReadOnlyList<string> Validos =
        ["normativa", "procedimiento_interno", ManualTecnico, "politica_seguridad", "otro"];

    public static bool EsValido(string? tipo) => tipo is not null && Validos.Contains(tipo);

    public static void Validar(DatosDelDocumento datos)
    {
        if (!EsValido(datos.Tipo))
        {
            throw new ValidacionDeNegocioException(
                "El tipo debe ser: " + string.Join(", ", Validos) + ".", "TIPO_DOCUMENTO_INVALIDO");
        }

        if (datos.Titulo is { Length: > LargoMaximoTitulo })
        {
            throw new ValidacionDeNegocioException(
                $"El título no puede superar {LargoMaximoTitulo} caracteres.", "TITULO_DEMASIADO_LARGO");
        }

        if (datos.Descripcion is { Length: > LargoMaximoDescripcion })
        {
            throw new ValidacionDeNegocioException(
                $"La descripción no puede superar {LargoMaximoDescripcion} caracteres.", "DESCRIPCION_DEMASIADO_LARGA");
        }
    }
}

/// <summary>Datos generales de un documento. El texto vacío se guarda como ausente.</summary>
/// <param name="Titulo">Título; si falta se usa el nombre del archivo.</param>
/// <param name="Tipo">Tipo de documento (por defecto <c>manual_tecnico</c>).</param>
/// <param name="Descripcion">Descripción opcional.</param>
/// <param name="ActivoId">Activo al que se asocia, opcional.</param>
/// <param name="ModeloMaquinaId">Modelo de máquina al que se asocia, opcional.</param>
public sealed record DatosDelDocumento(
    string? Titulo, string Tipo, string? Descripcion, long? ActivoId, long? ModeloMaquinaId)
{
    public DatosDelDocumento Normalizar() => this with
    {
        Titulo = string.IsNullOrWhiteSpace(Titulo) ? null : Titulo.Trim(),
        Descripcion = string.IsNullOrWhiteSpace(Descripcion) ? null : Descripcion.Trim(),
        Tipo = string.IsNullOrWhiteSpace(Tipo) ? TiposDeDocumento.ManualTecnico : Tipo.Trim(),
    };
}
