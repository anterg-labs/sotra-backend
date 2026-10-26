using Api.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NUnit.Framework;
using SOTRA_BACKEND.Operaciones;

namespace Api.PruebasUnitarias;

public class TraduccionDeErroresPruebas
{
    private static DbUpdateException Duplicado(string? tabla, string? restriccion) =>
        new("guardar", new PostgresException(
            "duplicate key", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation,
            tableName: tabla, constraintName: restriccion));

    [Test]
    public void UnConflictoDeDatosEs409ConSuCodigo()
    {
        var respuesta = TraduccionDeErroresDeNegocio.Traducir(new ConflictoDeDatosException("MODELO_DUPLICADO", "Ya existe"));

        Assert.That(respuesta, Is.EqualTo(new TraduccionDeErroresDeNegocio.Respuesta(409, "MODELO_DUPLICADO", "Ya existe")));
    }

    [Test]
    public void UnaValidacionDeNegocioEs400ConElCodigoPorDefectoOElPropio()
    {
        var comun = TraduccionDeErroresDeNegocio.Traducir(new ValidacionDeNegocioException("El cliente indicado no existe."));
        var propio = TraduccionDeErroresDeNegocio.Traducir(new ValidacionDeNegocioException("El activo no existe.", "ACTIVO_NO_EXISTE"));

        Assert.That(comun, Is.EqualTo(new TraduccionDeErroresDeNegocio.Respuesta(400, "SOLICITUD_INVALIDA", "El cliente indicado no existe.")));
        Assert.That(propio!.Value.Codigo, Is.EqualTo("ACTIVO_NO_EXISTE"));
    }

    [TestCase(ErrorDeOrdenException.OrdenCerrada, 409)]
    [TestCase(ErrorDeOrdenException.OrdenNoModificable, 409)]
    [TestCase(ErrorDeOrdenException.OrdenModificada, 412)]
    [TestCase(ErrorDeOrdenException.OrdenInexistente, 404)]
    [TestCase(ErrorDeOrdenException.TransicionInvalida, 400)]
    [TestCase(ErrorDeOrdenException.TransicionNoPermitida, 403)]
    [TestCase(ErrorDeOrdenException.TecnicoObligatorio, 400)]
    [TestCase(ErrorDeOrdenException.TecnicoYaAsignado, 409)]
    [TestCase(ErrorDeOrdenException.MotivoObligatorio, 400)]
    public void CadaErrorDeOrdenTieneSuEstadoHttpYUnMensajeClaro(string codigo, int estado)
    {
        var respuesta = TraduccionDeErroresDeNegocio.Traducir(new ErrorDeOrdenException(codigo));

        Assert.That(respuesta!.Value.Estado, Is.EqualTo(estado));
        Assert.That(respuesta.Value.Mensaje, Is.Not.Empty);
        Assert.That(respuesta.Value.Mensaje, Does.Not.Contain("_"));
    }

    [Test]
    public void UnCodigoDeOrdenDesconocidoNoSeTraduce()
    {
        Assert.That(TraduccionDeErroresDeNegocio.Traducir(new ErrorDeOrdenException("OTRO")), Is.Null);
    }

    [TestCase("cliente", "UK_RAZON_SOCIAL_CLIENTE", "RAZON_SOCIAL_DUPLICADA")]
    [TestCase("cliente", "UK_NOMBRE_CLIENTE", "NOMBRE_CONTACTO_DUPLICADO")]
    [TestCase("cliente", "UK_CORREO_CLIENTE", "CORREO_DUPLICADO")]
    [TestCase("cliente", "UK_TELEFONO_CLIENTE", "TELEFONO_DUPLICADO")]
    [TestCase("cliente", "UK_TELEFONO_CLIENTE_NORMALIZADO", "TELEFONO_DUPLICADO")]
    [TestCase("usuario", "UK_MAIL_USUARIO", "CORREO_DUPLICADO")]
    [TestCase("usuario", "UK_TELEFONO_USUARIO", "TELEFONO_DUPLICADO")]
    [TestCase("cliente", "RESTRICCION_NUEVA", "CLIENTE_DUPLICADO")]
    [TestCase("usuario", null, "USUARIO_DUPLICADO")]
    [TestCase("activo", "UK_NUM_SERIE_ACTIVO", "NUMERO_SERIE_DUPLICADO")]
    [TestCase("tipo_maquina", "UK_NOMBRE_TIPO_MAQUINA", "TIPO_MAQUINA_DUPLICADO")]
    [TestCase("modelo_maquina", "UX_MODELO_MAQUINA_NOMBRE_TIPO", "MODELO_DUPLICADO")]
    [TestCase("repuesto", "UK_CODIGO_REPUESTO", "CODIGO_REPUESTO_DUPLICADO")]
    [TestCase("otra_tabla", "UK_X", "DATO_DUPLICADO")]
    public void UnaRestriccionUnicaSeTraduceASuCodigoOAlDeSuTabla(string tabla, string? restriccion, string codigoEsperado)
    {
        var respuesta = TraduccionDeErroresDeNegocio.Traducir(Duplicado(tabla, restriccion));

        Assert.That(respuesta!.Value.Estado, Is.EqualTo(409));
        Assert.That(respuesta.Value.Codigo, Is.EqualTo(codigoEsperado));
    }

    [Test]
    public void OtroErrorDeBaseDeDatosNoSeTraduce()
    {
        var otro = new DbUpdateException("guardar", new PostgresException(
            "fk", "ERROR", "ERROR", PostgresErrorCodes.ForeignKeyViolation));

        Assert.That(TraduccionDeErroresDeNegocio.Traducir(otro), Is.Null);
    }

    [Test]
    public void UnaExcepcionInesperadaNoSeTraduceNiExponeSuTexto()
    {
        Assert.That(TraduccionDeErroresDeNegocio.Traducir(new InvalidOperationException("detalle interno")), Is.Null);
        Assert.That(TraduccionDeErroresDeNegocio.Traducir(new NullReferenceException()), Is.Null);
    }
}
