namespace SOTRA_BACKEND.Operaciones;

/// <summary>Indica que la operación choca con un dato existente. Lleva un código estable para la API.</summary>
public sealed class ConflictoDeDatosException(string codigo, string mensaje) : Exception(mensaje)
{
    public string Codigo { get; } = codigo;
}
