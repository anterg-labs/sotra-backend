using System.ComponentModel.DataAnnotations;
using Api.DTOs.Usuarios;
using Api.Mapeadores;
using SOTRA_BACKEND.Modelos;

namespace Api.PruebasUnitarias;

public class UsuariosPruebas
{
    private static UsuarioCrearDto CrearValido(
        string nombre = "Ana",
        string apellido = "Pérez",
        string correo = "ana@example.com",
        string telefono = "99123456",
        List<long>? idsRol = null)
        => new()
        {
            Nombre = nombre,
            Apellido = apellido,
            Correo = correo,
            Telefono = telefono,
            IdsRol = idsRol ?? [1]
        };

    private static UsuarioActualizarDto ActualizarValido(string telefono = "99123456")
        => new()
        {
            Nombre = "Ana",
            Apellido = "Pérez",
            Correo = "ana@example.com",
            Telefono = telefono,
            IdsRol = [1],
            Estado = "activo",
            FechaIngreso = new DateOnly(2026, 10, 21)
        };

    private static List<ValidationResult> Validar(object dto)
    {
        List<ValidationResult> errores = [];
        Validator.TryValidateObject(dto, new ValidationContext(dto), errores, validateAllProperties: true);
        return errores;
    }

    [Test]
    public void Alta_ConDatosValidos_NoTieneErrores()
    {
        Assert.That(Validar(CrearValido()), Is.Empty);
    }

    [Test]
    public void Alta_SinRoles_SeRechazaConElMensajeDelRol()
    {
        var errores = Validar(CrearValido(idsRol: []));

        Assert.That(errores.Select(e => e.ErrorMessage), Does.Contain("El usuario debe tener al menos un rol."));
    }

    [Test]
    public void Modificacion_SinRoles_SeRechazaConElMensajeDelRol()
    {
        var dto = new UsuarioActualizarDto
        {
            Nombre = "Ana",
            Apellido = "Pérez",
            Correo = "ana@example.com",
            Telefono = "99123456",
            IdsRol = [],
            Estado = "activo",
            FechaIngreso = new DateOnly(2026, 10, 21)
        };

        Assert.That(Validar(dto).Select(e => e.ErrorMessage), Does.Contain("El usuario debe tener al menos un rol."));
    }

    [TestCase("D'Angelo", "O’Brien")]
    [TestCase("María-José", "Núñez")]
    [TestCase("Jr.", "de la Cruz")]
    public void Alta_AceptaNombresConTildesApostrofosGuionesYPuntos(string nombre, string apellido)
    {
        Assert.That(Validar(CrearValido(nombre: nombre, apellido: apellido)), Is.Empty);
    }

    [TestCase("Ana1")]
    [TestCase("Ana@")]
    [TestCase("123")]
    [TestCase("   ")]
    public void Alta_RechazaNombresConCaracteresInvalidosOEnBlanco(string nombre)
    {
        Assert.That(Validar(CrearValido(nombre: nombre)), Is.Not.Empty);
    }

    [TestCase("9912345")]
    [TestCase("991234567")]
    [TestCase("+59899123456")]
    [TestCase("9912 345")]
    public void Alta_RechazaTelefonosFueraDelFormatoConCeroInicialOpcional(string telefono)
    {
        var errores = Validar(CrearValido(telefono: telefono));

        Assert.That(errores, Is.Not.Empty);
    }

    [TestCase("99123456")]
    [TestCase("099123456")]
    [TestCase("092624911")]
    public void Alta_AceptaElCeroInicialYConservaElFormatoIngresado(string telefono)
    {
        var datos = CrearValido(telefono: telefono);

        Assert.Multiple(() =>
        {
            Assert.That(Validar(datos), Is.Empty);
            Assert.That(UsuarioMapeador.AEntidad(datos).Telefono, Is.EqualTo(telefono));
        });
    }

    [TestCase("099123456")]
    [TestCase("092624911")]
    public void Modificacion_ConservaElFormatoIngresado(string telefono)
    {
        var datos = ActualizarValido(telefono: telefono);
        var usuario = UsuarioMapeador.AEntidad(CrearValido());

        Assert.That(Validar(datos), Is.Empty);
        UsuarioMapeador.AplicarCambios(usuario, datos);

        Assert.That(usuario.Telefono, Is.EqualTo(telefono));
    }

    [TestCase("sin-arroba")]
    [TestCase("ana@")]
    public void Alta_RechazaCorreosConFormatoInvalido(string correo)
    {
        var errores = Validar(CrearValido(correo: correo));

        Assert.That(errores.Select(e => e.ErrorMessage), Does.Contain("El correo no tiene un formato válido."));
    }

    [Test]
    public void Alta_RechazaCorreoDeMasDe254Caracteres()
    {
        var correo = new string('a', 250) + "@x.uy";

        var errores = Validar(CrearValido(correo: correo));

        Assert.That(errores.Select(e => e.ErrorMessage), Does.Contain("El correo no puede superar los 254 caracteres."));
    }

    [Test]
    public void Modificacion_ExigeElMismoTelefonoDeOchoDigitos()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Validar(ActualizarValido(telefono: "99123456")), Is.Empty);
            Assert.That(Validar(ActualizarValido(telefono: "9912345")), Is.Not.Empty);
        });
    }

    [Test]
    public void AplicarCambios_NoModificaLaIdentidadNiElIdDeKeycloak()
    {
        var idKeycloak = Guid.NewGuid();
        var usuario = new Usuario
        {
            IdUsuario = 7,
            Nombre = "a",
            Apellido = "b",
            Mail = "c@example.com",
            Telefono = "11111111",
            Estado = true,
            IdKeycloak = idKeycloak
        };

        UsuarioMapeador.AplicarCambios(usuario, ActualizarValido());

        Assert.Multiple(() =>
        {
            Assert.That(usuario.IdUsuario, Is.EqualTo(7));
            Assert.That(usuario.IdKeycloak, Is.EqualTo(idKeycloak));
            Assert.That(usuario.Nombre, Is.EqualTo("Ana"));
            Assert.That(usuario.Telefono, Is.EqualTo("99123456"));
            Assert.That(usuario.FechaIngreso, Is.EqualTo(new DateOnly(2026, 10, 21)));
        });
    }

    [Test]
    public void AEntidad_CreaElUsuarioActivoConLaFechaDeHoy()
    {
        var antes = DateOnly.FromDateTime(DateTime.UtcNow);

        var entidad = UsuarioMapeador.AEntidad(CrearValido());

        Assert.Multiple(() =>
        {
            Assert.That(entidad.Estado, Is.True);
            Assert.That(entidad.FechaIngreso, Is.GreaterThanOrEqualTo(antes));
            Assert.That(entidad.Mail, Is.EqualTo("ana@example.com"));
        });
    }
}
