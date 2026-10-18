using System.Text;
using Api.Servicios;

namespace Api.PruebasUnitarias;

public class ReportesPruebas
{
    private static TablaReporte Tabla(string titulo, string[] columnas, params string[][] filas) => new()
    {
        Titulo = titulo,
        Columnas = [.. columnas],
        Filas = [.. filas.Select(fila => fila.ToList())],
    };

    private static string Texto(byte[] contenido) => Encoding.UTF8.GetString(contenido, 3, contenido.Length - 3);

    // CSV

    [TestCase("hola", "hola")]
    [TestCase("", "")]
    [TestCase("a;b", "\"a;b\"")]
    [TestCase("di \"hola\"", "\"di \"\"hola\"\"\"")]
    [TestCase("línea1\nlínea2", "\"línea1\nlínea2\"")]
    [TestCase("a\r\nb", "\"a\r\nb\"")]
    [TestCase("a,b", "a,b")]
    public void Csv_Escapar_AplicaComillasDeRfc4180(string valor, string esperado)
    {
        Assert.That(GeneradorReportesCsv.Escapar(valor), Is.EqualTo(esperado));
    }

    [TestCase("=1+1", "'=1+1")]
    [TestCase("+54", "'+54")]
    [TestCase("-5", "'-5")]
    [TestCase("@SUMA(A1)", "'@SUMA(A1)")]
    [TestCase("\tcmd", "'\tcmd")]
    public void Csv_Escapar_NeutralizaInyeccionDeFormulas(string valor, string esperado)
    {
        Assert.That(GeneradorReportesCsv.Escapar(valor), Is.EqualTo(esperado));
    }

    [Test]
    public void Csv_Escapar_FormulaConSeparadorQuedaNeutralizadaYEntreComillas()
    {
        Assert.That(GeneradorReportesCsv.Escapar("=A1;B1"), Is.EqualTo("\"'=A1;B1\""));
    }

    [Test]
    public void Csv_Escapar_SoloNeutralizaAlInicio()
    {
        Assert.That(GeneradorReportesCsv.Escapar("a=b"), Is.EqualTo("a=b"));
        Assert.That(GeneradorReportesCsv.Escapar("x-y"), Is.EqualTo("x-y"));
    }

    [Test]
    public void Csv_Generar_EmpiezaConBomUtf8()
    {
        var bytes = GeneradorReportesCsv.Generar([Tabla("T", ["A"], ["1"])]);

        Assert.That(bytes.Take(3), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
    }

    [Test]
    public void Csv_Generar_UnaTablaNoLlevaTitulo()
    {
        var bytes = GeneradorReportesCsv.Generar([Tabla("Órdenes", ["Id", "Estado"], ["1", "creada"], ["2", "cerrada"])]);

        Assert.That(Texto(bytes), Is.EqualTo("Id;Estado\r\n1;creada\r\n2;cerrada\r\n"));
    }

    [Test]
    public void Csv_Generar_VariasTablasLlevanTituloYSeparacion()
    {
        var bytes = GeneradorReportesCsv.Generar(
        [
            Tabla("Uno", ["A"], ["1"]),
            Tabla("Dos", ["B"], ["x;y"]),
        ]);

        Assert.That(Texto(bytes), Is.EqualTo("Uno\r\nA\r\n1\r\n\r\nDos\r\nB\r\n\"x;y\"\r\n"));
    }

    [Test]
    public void Csv_Generar_TablaSinFilasSoloTieneEncabezado()
    {
        var bytes = GeneradorReportesCsv.Generar([Tabla("T", ["A", "B"])]);

        Assert.That(Texto(bytes), Is.EqualTo("A;B\r\n"));
    }

    [Test]
    public void Csv_Generar_SinTablasSoloTieneBom()
    {
        Assert.That(GeneradorReportesCsv.Generar([]), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
    }

    [Test]
    public void Csv_Generar_ConservaTildesYEscapaCeldasPeligrosas()
    {
        var bytes = GeneradorReportesCsv.Generar([Tabla("T", ["Descripción"], ["=HYPERLINK(\"x\")"], ["añadido"])]);

        var texto = Texto(bytes);
        Assert.That(texto, Does.Contain("Descripción"));
        Assert.That(texto, Does.Contain("\"'=HYPERLINK(\"\"x\"\")\""));
        Assert.That(texto, Does.Contain("añadido"));
    }

    // PDF

    [Test]
    public void Pdf_Generar_ProduceUnPdfValido()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var bytes = GeneradorReportesPdf.Generar("Órdenes", ["Desde: 2026-01-01"],
        [
            Tabla("Resumen", ["Id", "Estado"], ["1", "creada"], ["2", "cerrada"]),
        ]);

        Assert.That(bytes.Length, Is.GreaterThan(500));
        Assert.That(Encoding.ASCII.GetString(bytes, 0, 5), Is.EqualTo("%PDF-"));
    }

    [Test]
    public void Pdf_Generar_ToleraTablasSinFilasYSinCriterios()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var bytes = GeneradorReportesPdf.Generar("Vacío", [], [Tabla("Nada", ["A"])]);

        Assert.That(Encoding.ASCII.GetString(bytes, 0, 4), Is.EqualTo("%PDF"));
    }

    [Test]
    public void Pdf_Generar_ToleraMuchasFilasYTextoLargo()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var filas = Enumerable.Range(0, 300).Select(i => new[] { i.ToString(), new string('x', 400) }).ToArray();

        var bytes = GeneradorReportesPdf.Generar("Grande", ["c"], [Tabla("Filas", ["N", "Texto"], filas)]);

        Assert.That(Encoding.ASCII.GetString(bytes, 0, 4), Is.EqualTo("%PDF"));
    }

    [Test]
    public void HoraUruguay_RestaTresHoras()
    {
        var local = HoraUruguay.Convertir(new DateTime(2026, 6, 1, 15, 30, 0, DateTimeKind.Utc));

        Assert.That(local, Is.EqualTo(new DateTime(2026, 6, 1, 12, 30, 0)));
    }

    [Test]
    public void HoraUruguay_AceptaFechasSinTipoComoUtc()
    {
        var local = HoraUruguay.Convertir(new DateTime(2026, 6, 1, 0, 30, 0, DateTimeKind.Unspecified));

        Assert.That(local.Day, Is.EqualTo(31));
        Assert.That(local.Hour, Is.EqualTo(21));
    }
}
