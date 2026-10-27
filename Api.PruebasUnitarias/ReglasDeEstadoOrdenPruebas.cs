using SOTRA_BACKEND.Operaciones;

namespace Api.PruebasUnitarias;

public class ReglasDeEstadoOrdenPruebas
{
    private static readonly ReglasDeEstadoOrden Reglas = ReglasDePrueba.Documentadas();

    [Test]
    public void ElEstadoInicialEsElMarcadoEnElCatalogo()
    {
        Assert.That(Reglas.Inicial.Nombre, Is.EqualTo("creada"));
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

        Assert.That(reglas.Estados.Select(estado => estado.Orden), Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7 }).AsCollection);
    }

    [TestCase("cerrada", true)]
    [TestCase("cancelada", true)]
    [TestCase("finalizada", false)]
    [TestCase("creada", false)]
    [TestCase("desconocido", false)]
    public void UnEstadoFinalEsElQueNoTieneTransicionesDeSalida(string estado, bool esperado)
    {
        Assert.That(Reglas.EsFinal(estado), Is.EqualTo(esperado));
    }

    [TestCase("cerrada", true)]
    [TestCase("cancelada", false)]
    [TestCase("finalizada", false)]
    public void SoloLaFaseCerradaDevuelveOrdenCerrada(string estado, bool esperado)
    {
        Assert.That(Reglas.EstaCerrada(estado), Is.EqualTo(esperado));
    }

    [TestCase("creada", "asignada")]
    [TestCase("asignada", null)]
    [TestCase("finalizada", null)]
    [TestCase("desconocido", null)]
    public void AsignarUnTecnicoLlevaALaTransicionMarcadaYSiNoHayDejaElEstado(string actual, string? esperado)
    {
        Assert.That(Reglas.EstadoAlAsignarTecnico(actual), Is.EqualTo(esperado));
    }

    [TestCase("en_ejecucion", "inicio")]
    [TestCase("cerrada", "cierre")]
    [TestCase("pausada", null)]
    [TestCase("desconocido", null)]
    public void CadaEstadoDefineQueFechaCompleta(string estado, string? esperada)
    {
        Assert.That(Reglas.CompletaFecha(estado), Is.EqualTo(esperada));
    }

    [Test]
    public void LasAccionesDelTecnicoSalenDeLasTransiciones()
    {
        var acciones = Reglas.TransicionesDesde("en_ejecucion").Where(t => t.PuedeTecnico).Select(t => (t.Destino, t.Accion, t.Principal));

        Assert.That(acciones, Is.EqualTo(new[] { ("pausada", "Pausar", false), ("finalizada", "Finalizar", true) }));
        Assert.That(Reglas.TransicionesDesde("pausada").Single(t => t.PuedeTecnico).Accion, Is.EqualTo("Reanudar"));
        Assert.That(Reglas.TransicionesDesde("asignada").Single(t => t.PuedeTecnico).Accion, Is.EqualTo("Iniciar"));
        Assert.That(Reglas.TransicionesDesde("finalizada").Any(t => t.PuedeTecnico), Is.False);
    }

    [TestCase("asignada", "en_ejecucion")]
    [TestCase("en_ejecucion", "pausada")]
    [TestCase("en_ejecucion", "finalizada")]
    [TestCase("pausada", "en_ejecucion")]
    public void UnTecnicoPuedeHacerSoloLasTransicionesMarcadasParaEl(string actual, string nuevo)
    {
        Assert.DoesNotThrow(() => Reglas.ValidarTransicion(actual, nuevo, 7, "motivo", comoTecnico: true));
    }

    [TestCase("finalizada", "cerrada")]
    [TestCase("finalizada", "asignada")]
    [TestCase("creada", "asignada")]
    [TestCase("creada", "cancelada")]
    [TestCase("asignada", "cancelada")]
    [TestCase("pausada", "cancelada")]
    public void UnTecnicoNoPuedeCerrarCancelarNiAsignar(string actual, string nuevo)
    {
        var error = Assert.Throws<ErrorDeOrdenException>(() => Reglas.ValidarTransicion(actual, nuevo, 7, "motivo", comoTecnico: true));

        Assert.That(error!.Codigo, Is.EqualTo(ErrorDeOrdenException.TransicionNoPermitida));
        Assert.DoesNotThrow(() => Reglas.ValidarTransicion(actual, nuevo, 7, "motivo", comoTecnico: false));
    }

    [Test]
    public void ParaUnTecnicoUnaTransicionInexistenteSigueSiendoInvalida()
    {
        var error = Assert.Throws<ErrorDeOrdenException>(() => Reglas.ValidarTransicion("creada", "cerrada", 7, "motivo", comoTecnico: true));

        Assert.That(error!.Codigo, Is.EqualTo(ErrorDeOrdenException.TransicionInvalida));
    }

    [Test]
    public void UnaTransicionHaciaUnEstadoInexistenteSeIgnora()
    {
        var conSobrante = ReglasDePrueba.TransicionesDocumentadas.Append(new TransicionDeEstado("creada", "inventado", false, false, null, false, false));

        var reglas = new ReglasDeEstadoOrden(ReglasDePrueba.EstadosDocumentados, conSobrante);

        Assert.That(reglas.Siguientes("creada"), Is.EqualTo(new[] { "asignada", "cancelada" }));
    }

    [Test]
    public void LaFaseDeUnEstadoSaleDelCatalogo()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Reglas.FaseDe("creada"), Is.EqualTo(FasesDeEstado.Pendiente));
            Assert.That(Reglas.FaseDe("pausada"), Is.EqualTo(FasesDeEstado.EnCurso));
            Assert.That(Reglas.FaseDe("finalizada"), Is.EqualTo(FasesDeEstado.PorValidar));
            Assert.That(Reglas.FaseDe("desconocido"), Is.Null);
        });
    }
}
