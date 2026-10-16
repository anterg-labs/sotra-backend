using System.ComponentModel.DataAnnotations;

namespace Api.Validaciones;

/// <summary>Exige al menos 8 caracteres, una mayúscula, una minúscula y un carácter especial.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class ContrasenaSeguraAttribute : ValidationAttribute
{
    public const int LongitudMinima = 8;

    protected override ValidationResult? IsValid(object? valor, ValidationContext contexto)
    {
        if (valor is not string contrasena)
        {
            return ValidationResult.Success;
        }

        if (contrasena.Length == 0)
        {
            return new ValidationResult("El campo Contraseña es obligatorio.");
        }

        if (contrasena.Length < LongitudMinima)
        {
            return new ValidationResult($"La contraseña debe tener al menos {LongitudMinima} caracteres.");
        }

        if (!contrasena.Any(char.IsUpper))
        {
            return new ValidationResult("La contraseña debe contener al menos una letra mayúscula.");
        }

        if (!contrasena.Any(char.IsLower))
        {
            return new ValidationResult("La contraseña debe contener al menos una letra minúscula.");
        }

        if (!contrasena.Any(caracter => !char.IsLetterOrDigit(caracter) && !char.IsWhiteSpace(caracter)))
        {
            return new ValidationResult("La contraseña debe contener al menos un carácter especial.");
        }

        return ValidationResult.Success;
    }
}
