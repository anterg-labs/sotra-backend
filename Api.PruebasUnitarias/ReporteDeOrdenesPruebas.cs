using Api.Controladores;
using SOTRA_BACKEND.Operaciones;

namespace Api.PruebasUnitarias;

public class ReporteDeOrdenesPruebas
{
    private static List<OrdenResumen> CrearOrdenes(int cantidad) =>
        Enumerable.Range(1, cantidad)
            .Select(i => new OrdenResumen(i, DateTime.UtcNow, null, null, 1, "Cliente", 1, "Activo", null, null, false, 3, "creada"))
            .ToList();

    [Test]
    public void ConMasDeCincoMilOrdenesElListadoSeRecortaYElTituloLoAvisa()
    {
        var tabla = ReportesController.ArmarTablaOrdenes(CrearOrdenes(ReportesController.MaximoDeOrdenesListadas + 20));

        Assert.That(tabla.Filas, Has.Count.EqualTo(ReportesController.MaximoDeOrdenesListadas));
        Assert.That(tabla.Titulo, Does.Contain("primeras 5000 de 5020"));
    }

    [Test]
    public void ConElMaximoExactoNoSeRecorta()
    {
        var tabla = ReportesController.ArmarTablaOrdenes(CrearOrdenes(ReportesController.MaximoDeOrdenesListadas));

        Assert.That(tabla.Filas, Has.Count.EqualTo(ReportesController.MaximoDeOrdenesListadas));
        Assert.That(tabla.Titulo, Is.EqualTo("Órdenes de trabajo (5000)"));
    }
}
