using System.ComponentModel.DataAnnotations;

namespace Api.Validaciones;

/// <summary>
/// Valida nombres y apellidos de personas: sin texto en blanco ni símbolos sueltos.
/// Se permiten letras (con tildes), espacios, apóstrofo, guion y punto, y debe haber al menos una letra.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NombrePersonaAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? valor, ValidationContext contexto)
    {
        if (valor is null)
        {
            return ValidationResult.Success;
        }

        var texto = valor as string ?? "";
        var campo = contexto.DisplayName;

        if (string.IsNullOrWhiteSpace(texto))
        {
            return new ValidationResult($"El campo {campo} es obligatorio.");
        }

        var contieneLetra = false;
        foreach (var caracter in texto)
        {
            if (char.IsLetter(caracter))
            {
                contieneLetra = true;
                continue;
            }

            if (caracter is ' ' or '\'' or '’' or '-' or '.')
            {
                continue;
            }

            return new ValidationResult(
                $"El campo {campo} solo admite letras, espacios, apóstrofos, guiones y puntos.");
        }

        return contieneLetra
            ? ValidationResult.Success
            : new ValidationResult($"El campo {campo} debe contener al menos una letra.");
    }
}
