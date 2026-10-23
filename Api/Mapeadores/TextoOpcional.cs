namespace Api.Mapeadores;

public static class TextoOpcional
{
    public static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor;
}
