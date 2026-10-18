using System.ComponentModel.DataAnnotations;

namespace Api.Validaciones;

/// <summary>Valida que el teléfono esté formado únicamente por dígitos. Con <see cref="Obligatorio"/> en falso admite vacío.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class TelefonoAttribute : ValidationAttribute
{
    public bool Obligatorio { get; init; } = true;

    protected override ValidationResult? IsValid(object? valor, ValidationContext contexto)
    {
        if (valor is not string texto)
        {
            return ValidationResult.Success;
        }

        if (string.IsNullOrWhiteSpace(texto))
        {
            return Obligatorio
                ? new ValidationResult("El campo Teléfono es obligatorio.")
                : ValidationResult.Success;
        }

        return texto.All(char.IsAsciiDigit)
            ? ValidationResult.Success
            : new ValidationResult("El campo Teléfono solo admite dígitos.");
    }
}
