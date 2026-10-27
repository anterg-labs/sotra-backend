using Api.Servicios;

namespace Api.PruebasUnitarias;

public class ProcesamientoDeDocumentosPruebas
{
    [Test]
    public async Task LaColaEntregaLosDocumentosEnElOrdenEnQueLlegaron()
    {
        var cola = new ColaDeDocumentosTecnicos();
        cola.Encolar(3);
        cola.Encolar(1);
        cola.Encolar(2);

        using var cancelacion = new CancellationTokenSource();
        var recibidos = new List<long>();
        await foreach (var id in cola.LeerAsync(cancelacion.Token))
        {
            recibidos.Add(id);
            if (recibidos.Count == 3)
            {
                break;
            }
        }

        Assert.That(recibidos, Is.EqualTo(new long[] { 3, 1, 2 }));
    }

    [TestCase(ErroresDeProcesamiento.PdfInvalido)]
    [TestCase(ErroresDeProcesamiento.PdfSinTexto)]
    [TestCase(ErroresDeProcesamiento.ServicioIaNoDisponible)]
    [TestCase(ErroresDeProcesamiento.ProcesamientoFallido)]
    public void CadaErrorDelProcesamientoTieneUnMensajeParaLaPersona(string codigo)
    {
        Assert.That(ErroresDeProcesamiento.Mensaje(codigo), Is.Not.Empty.And.EndsWith("."));
    }

    [Test]
    public void UnCodigoDesconocidoMuestraElMensajeGeneral()
    {
        Assert.That(ErroresDeProcesamiento.Mensaje("OTRO"),
            Is.EqualTo(ErroresDeProcesamiento.Mensaje(ErroresDeProcesamiento.ProcesamientoFallido)));
    }
}
