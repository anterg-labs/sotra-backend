using System.ComponentModel.DataAnnotations;
using Api.DTOs.Clientes;

namespace Api.PruebasUnitarias;

public class ClientesPruebas
{
    private static List<ValidationResult> Validar(object solicitud)
    {
        List<ValidationResult> errores = [];
        Validator.TryValidateObject(solicitud, new ValidationContext(solicitud), errores, validateAllProperties: true);
        return errores;
    }

    private static ClienteCrearDto SolicitudValida(
        string razonSocial = "Cliente de prueba",
        string nombreContacto = "Ana Pérez",
        string? correo = "contacto@example.com",
        string? telefono = "99123456",
        string? direccion = "Calle 123") => new()
        {
            RazonSocial = razonSocial,
            NombreContacto = nombreContacto,
            Correo = correo,
            Telefono = telefono,
            Direccion = direccion,
        };

    private static void ComprobarErrorUnico(object solicitud, string campo, string mensaje)
    {
        var errores = Validar(solicitud);
        var contexto = new ValidationContext(solicitud) { MemberName = campo };
        var valor = solicitud.GetType().GetProperty(campo)!.GetValue(solicitud);
        List<ValidationResult> erroresDelCampo = [];
        var campoValido = Validator.TryValidateProperty(valor, contexto, erroresDelCampo);
        Assert.That(errores, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(campoValido, Is.False);
            Assert.That(erroresDelCampo.Select(error => error.ErrorMessage), Is.EqualTo(new[] { mensaje }));
            Assert.That(errores[0].ErrorMessage, Is.EqualTo(mensaje));
        });
    }

    [Test]
    public void AltaAceptaDatosValidosEnLosLimitesDeLongitud()
    {
        var solicitud = SolicitudValida(
            razonSocial: new string('R', 160),
            nombreContacto: new string('N', 120),
            direccion: new string('D', 255));

        Assert.That(Validar(solicitud), Is.Empty);
    }

    [Test]
    public void AltaPermiteOmitirTodosLosCamposOpcionales()
    {
        var solicitud = SolicitudValida(correo: null, telefono: null, direccion: null);

        Assert.That(Validar(solicitud), Is.Empty);
    }

    [TestCase("RazonSocial", "El campo Nombre o razón social es obligatorio.")]
    [TestCase("NombreContacto", "El campo Nombre de contacto es obligatorio.")]
    public void AltaRechazaCamposObligatoriosEnBlanco(string campo, string mensaje)
    {
        var solicitud = campo == "RazonSocial"
            ? SolicitudValida(razonSocial: "   ")
            : SolicitudValida(nombreContacto: "");

        ComprobarErrorUnico(solicitud, campo, mensaje);
    }

    [TestCase("RazonSocial", "El campo Nombre o razón social no puede superar los 160 caracteres.")]
    [TestCase("NombreContacto", "El campo Nombre de contacto no puede superar los 120 caracteres.")]
    [TestCase("Direccion", "La dirección no puede superar los 255 caracteres.")]
    public void AltaRechazaLongitudesQueSuperanElMaximoPorUnCaracter(string campo, string mensaje)
    {
        var solicitud = campo switch
        {
            "RazonSocial" => SolicitudValida(razonSocial: new string('R', 161)),
            "NombreContacto" => SolicitudValida(nombreContacto: new string('N', 121)),
            _ => SolicitudValida(direccion: new string('D', 256)),
        };

        ComprobarErrorUnico(solicitud, campo, mensaje);
    }

    [Test]
    public void AltaRechazaCorreoSinFormatoValido()
    {
        ComprobarErrorUnico(SolicitudValida(correo: "sin-arroba"), "Correo", "El correo no tiene un formato válido.");
    }

    [TestCase("9912345", "El campo teléfono debe tener 8 dígitos (o 9 si empieza con 0).")]
    [TestCase("991234567", "El campo teléfono debe tener 8 dígitos (o 9 si empieza con 0).")]
    [TestCase("abcdefgh", "El campo teléfono solo admite dígitos.")]
    public void AltaRechazaTelefonoInformadoInvalido(string telefono, string mensaje)
    {
        ComprobarErrorUnico(SolicitudValida(telefono: telefono), "Telefono", mensaje);
    }

    [Test]
    public void ModificacionTambienRechazaTelefonoDeNueveDigitos()
    {
        var solicitud = new ClienteActualizarDto
        {
            RazonSocial = "Cliente actualizado",
            NombreContacto = "Ana Pérez",
            Correo = "contacto@example.com",
            Telefono = "991234567",
            Direccion = "Calle 456",
        };

        ComprobarErrorUnico(solicitud, "Telefono", "El campo teléfono debe tener 8 dígitos (o 9 si empieza con 0).");
    }
}
