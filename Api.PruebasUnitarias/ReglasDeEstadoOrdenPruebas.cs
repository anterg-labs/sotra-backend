using SOTRA_BACKEND.Operaciones;

namespace Api.PruebasUnitarias;

public class ReglasDeEstadoOrdenPruebas
{
    private static readonly ReglasDeEstadoOrden Reglas = ReglasDePrueba.Documentadas();

    [Test]
    public void ElEstadoInicialEsElMarcadoEnElCatalogo()
    {
        Assert.That(Reglas.Inicial.Nombre, Is.EqualTo("pendiente"));
    }

    [Test]
    public void SinEstadoInicialElCatalogoEsInvalido()
    {
        var sinInicial = ReglasDePrueba.EstadosDocumentados.Select(estado => estado with { EsInicial = false });

        Assert.Throws<InvalidOperationException>(() => new ReglasDeEstadoOrden(sinInicial, ReglasDePrueba.TransicionesDocumentadas));
    }

    [Test]
    public void LosEstadosSalenEnElOrdenDelCatalogoAunqueLleguenDesordenados()
    {
        var desordenados = ReglasDePrueba.EstadosDocumentados.Reverse();

        var reglas = new ReglasDeEstadoOrden(desordenados, ReglasDePrueba.TransicionesDocumentadas);

        Assert.That(reglas.Estados.Select(estado => estado.Orden), Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6 }).AsCollection);
    }

    [TestCase("cerrado", true)]
    [TestCase("eliminado", true)]
    [TestCase("en_revision", false)]
    [TestCase("pendiente", false)]
    [TestCase("desconocido", false)]
    public void UnEstadoFinalEsElQueNoTieneTransicionesDeSalida(string estado, bool esperado)
    {
        Assert.That(Reglas.EsFinal(estado), Is.EqualTo(esperado));
    }

    [TestCase("cerrado", true)]
    [TestCase("eliminado", false)]
    [TestCase("en_revision", false)]
    public void SoloLaFaseCerradaDevuelveOrdenCerrada(string estado, bool esperado)
    {
        Assert.That(Reglas.EstaCerrada(estado), Is.EqualTo(esperado));
    }

    [TestCase("pendiente")]
    [TestCase("en_progreso")]
    [TestCase("en_revision")]
    [TestCase("desconocido")]
    public void AsignarUnTecnicoNoCambiaElEstadoSiNingunaTransicionLoPide(string actual)
    {
        Assert.That(Reglas.EstadoAlAsignarTecnico(actual), Is.Null);
    }

    [TestCase("en_progreso", "inicio")]
    [TestCase("cerrado", "cierre")]
    [TestCase("pausado", null)]
    [TestCase("desconocido", null)]
    public void CadaEstadoDefineQueFechaCompleta(string estado, string? esperada)
    {
        Assert.That(Reglas.CompletaFecha(estado), Is.EqualTo(esperada));
    }

    [Test]
    public void LasAccionesDelTecnicoSalenDeLasTransiciones()
    {
        var acciones = Reglas.TransicionesDesde("en_progreso").Where(t => t.PuedeTecnico).Select(t => (t.Destino, t.Accion, t.Principal));

        Assert.That(acciones, Is.EqualTo(new[] { ("en_revision", "Enviar a revisión", true), ("pausado", "Pausar", false) }));
        Assert.That(Reglas.TransicionesDesde("pausado").Single(t => t.PuedeTecnico).Accion, Is.EqualTo("Reanudar"));
        Assert.That(Reglas.TransicionesDesde("pendiente").Single(t => t.PuedeTecnico).Accion, Is.EqualTo("Iniciar"));
        Assert.That(Reglas.TransicionesDesde("en_revision").Any(t => t.PuedeTecnico), Is.False);
    }

    [TestCase("pendiente", "en_progreso")]
    [TestCase("en_progreso", "pausado")]
    [TestCase("en_progreso", "en_revision")]
    [TestCase("pausado", "en_progreso")]
    public void UnTecnicoPuedeHacerSoloLasTransicionesMarcadasParaEl(string actual, string nuevo)
    {
        Assert.DoesNotThrow(() => Reglas.ValidarTransicion(actual, nuevo, 7, "motivo", comoTecnico: true));
    }

    [TestCase("en_revision", "cerrado")]
    [TestCase("en_revision", "pendiente")]
    [TestCase("pendiente", "eliminado")]
    [TestCase("pausado", "eliminado")]
    public void UnTecnicoNoPuedeCerrarEliminarNiDevolverAPendiente(string actual, string nuevo)
    {
        var error = Assert.Throws<ErrorDeOrdenException>(() => Reglas.ValidarTransicion(actual, nuevo, 7, "motivo", comoTecnico: true));

        Assert.That(error!.Codigo, Is.EqualTo(ErrorDeOrdenException.TransicionNoPermitida));
        Assert.DoesNotThrow(() => Reglas.ValidarTransicion(actual, nuevo, 7, "motivo", comoTecnico: false));
    }

    [Test]
    public void ParaUnTecnicoUnaTransicionInexistenteSigueSiendoInvalida()
    {
        var error = Assert.Throws<ErrorDeOrdenException>(() => Reglas.ValidarTransicion("pendiente", "cerrado", 7, "motivo", comoTecnico: true));

        Assert.That(error!.Codigo, Is.EqualTo(ErrorDeOrdenException.TransicionInvalida));
    }

    [Test]
    public void UnaTransicionHaciaUnEstadoInexistenteSeIgnora()
    {
        var conSobrante = ReglasDePrueba.TransicionesDocumentadas.Append(new TransicionDeEstado("pendiente", "inventado", false, false, null, false, false));

        var reglas = new ReglasDeEstadoOrden(ReglasDePrueba.EstadosDocumentados, conSobrante);

        Assert.That(reglas.Siguientes("pendiente"), Is.EqualTo(new[] { "en_progreso", "eliminado" }));
    }

    [Test]
    public void LaFaseDeUnEstadoSaleDelCatalogo()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Reglas.FaseDe("pendiente"), Is.EqualTo(FasesDeEstado.Pendiente));
            Assert.That(Reglas.FaseDe("pausado"), Is.EqualTo(FasesDeEstado.EnCurso));
            Assert.That(Reglas.FaseDe("en_revision"), Is.EqualTo(FasesDeEstado.PorValidar));
            Assert.That(Reglas.FaseDe("eliminado"), Is.EqualTo(FasesDeEstado.Eliminada));
            Assert.That(Reglas.FaseDe("desconocido"), Is.Null);
        });
    }
}
