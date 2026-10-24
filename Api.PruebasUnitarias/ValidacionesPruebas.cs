using System.ComponentModel.DataAnnotations;
using Api.Validaciones;

namespace Api.PruebasUnitarias;

public class ValidacionesPruebas
{
    private static ValidationResult? Validar(ValidationAttribute atributo, object? valor, string campo = "Nombre")
    {
        var contexto = new ValidationContext(new object()) { DisplayName = campo };
        return atributo.GetValidationResult(valor, contexto);
    }

    [Test]
    public void ContrasenaSegura_AceptaContrasenaValida()
    {
        Assert.That(Validar(new ContrasenaSeguraAttribute(), "Abcdef1!"), Is.EqualTo(ValidationResult.Success));
    }

    [Test]
    public void ContrasenaSegura_IgnoraNulosYTiposQueNoSonTexto()
    {
        Assert.That(Validar(new ContrasenaSeguraAttribute(), null), Is.EqualTo(ValidationResult.Success));
        Assert.That(Validar(new ContrasenaSeguraAttribute(), 5), Is.EqualTo(ValidationResult.Success));
    }

    [TestCase("", "obligatorio")]
    [TestCase("Ab1!", "al menos 8")]
    [TestCase("Abcdef!", "al menos 8")]
    [TestCase("abcdefg!", "mayúscula")]
    [TestCase("ABCDEFG!", "minúscula")]
    [TestCase("Abcdefgh", "especial")]
    [TestCase("Abcdef12", "especial")]
    [TestCase("Abcdef 1", "especial")]
    public void ContrasenaSegura_RechazaContrasenasDebiles(string contrasena, string fragmentoDelMensaje)
    {
        var resultado = Validar(new ContrasenaSeguraAttribute(), contrasena);

        Assert.That(resultado, Is.Not.EqualTo(ValidationResult.Success));
        Assert.That(resultado!.ErrorMessage, Does.Contain(fragmentoDelMensaje));
    }

    [Test]
    public void ContrasenaSegura_LongitudMinimaExactaEsValida()
    {
        Assert.That(Validar(new ContrasenaSeguraAttribute(), "Aa!aaaaa"), Is.EqualTo(ValidationResult.Success));
        Assert.That(Validar(new ContrasenaSeguraAttribute(), "Aa!aaaa"), Is.Not.EqualTo(ValidationResult.Success));
    }

    [TestCase("Ana")]
    [TestCase("María José")]
    [TestCase("O'Brien")]
    [TestCase("D’Angelo")]
    [TestCase("Pérez-Gómez")]
    [TestCase("Jr.")]
    [TestCase("Ñandú")]
    public void NombrePersona_AceptaNombresValidos(string nombre)
    {
        Assert.That(Validar(new NombrePersonaAttribute(), nombre), Is.EqualTo(ValidationResult.Success));
    }

    [Test]
    public void NombrePersona_AceptaNulo()
    {
        Assert.That(Validar(new NombrePersonaAttribute(), null), Is.EqualTo(ValidationResult.Success));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\t")]
    public void NombrePersona_RechazaTextoEnBlancoConMensajeDeObligatorio(string nombre)
    {
        var resultado = Validar(new NombrePersonaAttribute(), nombre, "Apellido");

        Assert.That(resultado!.ErrorMessage, Is.EqualTo("El campo Apellido es obligatorio."));
    }

    [TestCase("Ana1")]
    [TestCase("Ana_")]
    [TestCase("<script>")]
    [TestCase("Ana\0")]
    [TestCase("Ana@")]
    public void NombrePersona_RechazaSimbolosYDigitos(string nombre)
    {
        var resultado = Validar(new NombrePersonaAttribute(), nombre);

        Assert.That(resultado!.ErrorMessage, Does.Contain("solo admite letras"));
    }

    [TestCase("-")]
    [TestCase("...")]
    [TestCase("' -")]
    public void NombrePersona_ExigeAlMenosUnaLetra(string nombre)
    {
        var resultado = Validar(new NombrePersonaAttribute(), nombre);

        Assert.That(resultado!.ErrorMessage, Does.Contain("al menos una letra"));
    }

    [Test]
    public void NombrePersona_ValorQueNoEsTextoSeTrataComoVacio()
    {
        Assert.That(Validar(new NombrePersonaAttribute(), 42)!.ErrorMessage, Does.Contain("obligatorio"));
    }

    [TestCase("99123456")]
    [TestCase("09912345")]
    [TestCase("00000000")]
    public void Telefono_AceptaExactamenteOchoDigitos(string telefono)
    {
        Assert.That(Validar(new TelefonoAttribute(), telefono), Is.EqualTo(ValidationResult.Success));
    }

    [TestCase("+598 99 123 456")]
    [TestCase("099-1234")]
    [TestCase("1234567a")]
    [TestCase("١٢٣٤٥٦٧٨")]
    [TestCase("1234567\n")]
    public void Telefono_RechazaCualquierCosaQueNoSeanDigitosAscii(string telefono)
    {
        Assert.That(Validar(new TelefonoAttribute(), telefono)!.ErrorMessage, Is.EqualTo("El campo teléfono solo admite dígitos."));
    }

    [TestCase("099123456")]
    [TestCase("092624911")]
    public void Telefono_AceptaUnCelularDeNueveDigitosConCeroInicial(string telefono)
    {
        Assert.That(Validar(new TelefonoAttribute(), telefono), Is.EqualTo(ValidationResult.Success));
    }

    [TestCase("1234567")]
    [TestCase("123456789")]
    [TestCase("0")]
    [TestCase("0991234567")]
    public void Telefono_RechazaUnLargoDistintoDeOchoDigitos(string telefono)
    {
        Assert.That(Validar(new TelefonoAttribute(), telefono)!.ErrorMessage, Is.EqualTo("El campo teléfono debe tener 8 dígitos (o 9 si empieza con 0)."));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Telefono_VacioEsErrorSiEsObligatorio(string telefono)
    {
        Assert.That(Validar(new TelefonoAttribute(), telefono)!.ErrorMessage, Is.EqualTo("El campo teléfono es obligatorio."));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void Telefono_VacioEsValidoSiNoEsObligatorio(string? telefono)
    {
        Assert.That(Validar(new TelefonoAttribute { Obligatorio = false }, telefono), Is.EqualTo(ValidationResult.Success));
    }

    [Test]
    public void Telefono_NuloSiempreEsValidoPorqueLoCubreRequired()
    {
        Assert.That(Validar(new TelefonoAttribute(), null), Is.EqualTo(ValidationResult.Success));
    }
}
