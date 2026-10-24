namespace Api.Validaciones;

/// <summary>
/// Números de teléfono de Uruguay: 8 dígitos (91 234 567), o 9 si es un celular escrito con el 0 inicial (091 234 567).
/// Usuarios conserva el formato ingresado y compara ambas variantes para evitar duplicados;
/// clientes utiliza <see cref="Normalizar"/> para guardar la forma de 8 dígitos.
/// </summary>
public static class NumerosDeTelefono
{
    public const int CantidadDeDigitos = 8;

    public static bool EsValido(string telefono)
    {
        return telefono.All(char.IsAsciiDigit)
            && (telefono.Length == CantidadDeDigitos || (telefono.Length == CantidadDeDigitos + 1 && telefono[0] == '0'));
    }

    /// <summary>Quita el 0 inicial de un celular de 9 dígitos (<c>092624911</c> pasa a <c>92624911</c>); el resto queda igual.</summary>
    public static string Normalizar(string telefono)
    {
        return telefono.Length == CantidadDeDigitos + 1 && telefono[0] == '0' ? telefono[1..] : telefono;
    }
}
