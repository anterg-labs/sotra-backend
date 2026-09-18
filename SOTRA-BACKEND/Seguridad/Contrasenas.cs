using Microsoft.AspNetCore.Identity;

namespace SOTRA_BACKEND.Seguridad;

public static class Contrasenas
{
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object Usuario = new();

    public static string CrearHash(string contrasena)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contrasena);
        return Hasher.HashPassword(Usuario, contrasena);
    }

    public static bool Verificar(string hash, string contrasena)
    {
        if (string.IsNullOrWhiteSpace(hash) || string.IsNullOrEmpty(contrasena)) return false;
        try
        {
            return Hasher.VerifyHashedPassword(Usuario, hash, contrasena)
                != PasswordVerificationResult.Failed;
        }
        catch (FormatException) { return false; }
    }
}
