using System.ComponentModel.DataAnnotations;

namespace Api.Validaciones;

/// <summary>Valida el teléfono: 8 dígitos, o 9 si empieza con 0 (celular con el 0 inicial; ver <see cref="NumerosDeTelefono"/>). El texto vacío o en blanco no se informa aquí: si el teléfono es obligatorio, lo dice <c>[Required]</c>.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class TelefonoAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? valor, ValidationContext contexto)
    {
        if (valor is not string texto)
        {
            return ValidationResult.Success;
        }

        if (string.IsNullOrWhiteSpace(texto))
        {
            return ValidationResult.Success;
        }

        if (!texto.All(char.IsAsciiDigit))
        {
            return new ValidationResult("El campo teléfono solo admite dígitos.");
        }

        return NumerosDeTelefono.EsValido(texto)
            ? ValidationResult.Success
            : new ValidationResult("El campo teléfono debe tener 8 dígitos (o 9 si empieza con 0).");
    }
}
