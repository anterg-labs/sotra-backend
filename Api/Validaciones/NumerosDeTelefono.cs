namespace Api.Validaciones;

/// <summary>
/// Números de teléfono de Uruguay: 8 dígitos (91 234 567), o 9 si es un celular escrito con el 0 inicial (091 234 567).
/// Usuarios y clientes conservan el formato ingresado y la base compara ambas variantes para evitar duplicados.
/// </summary>
public static class NumerosDeTelefono
{
    public const int CantidadDeDigitos = 8;

    public static bool EsValido(string telefono)
    {
        return telefono.All(char.IsAsciiDigit)
            && (telefono.Length == CantidadDeDigitos || (telefono.Length == CantidadDeDigitos + 1 && telefono[0] == '0'));
    }
}
