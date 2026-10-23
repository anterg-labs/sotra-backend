using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Api.Servicios;

public class TablaReporte
{
    public required string Titulo { get; init; }
    public List<string> Columnas { get; init; } = new();
    public List<List<string>> Filas { get; init; } = new();
}

public static class GeneradorReportesPdf
{
    // Tonos provisorios de la marca (negro y rojo) hasta contar con el manual de CVM; se reemplazan solo acá.
    private const string ColorMarcaNegro = "#111113";
    private const string ColorMarcaRojo = "#D71920";

    private static readonly Lazy<byte[]> Logo = new(CargarLogo);

    private static byte[] CargarLogo()
    {
        using var flujo = typeof(GeneradorReportesPdf).Assembly.GetManifestResourceStream("logo-cvm.png")
            ?? throw new InvalidOperationException("Falta el logo de CVM embebido en la aplicación.");
        using var memoria = new MemoryStream();
        flujo.CopyTo(memoria);
        return memoria.ToArray();
    }

    public static byte[] Generar(string titulo, List<string> criterios, List<TablaReporte> tablas) =>
        CrearDocumento(titulo, criterios, tablas).GeneratePdf();

    internal static Document CrearDocumento(string titulo, List<string> criterios, List<TablaReporte> tablas)
    {
        var fechaGeneracion = HoraUruguay.Convertir(DateTime.UtcNow).ToString("dd/MM/yyyy HH:mm");

        var documento = Document.Create(contenedor =>
        {
            contenedor.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(30);
                pagina.DefaultTextStyle(estilo => estilo.FontSize(9));

                pagina.Header().Column(columna =>
                {
                    columna.Item().Row(fila =>
                    {
                        fila.RelativeItem().AlignMiddle().Text("SOTRA - " + titulo).FontSize(16).Bold().FontColor(ColorMarcaNegro);
                        fila.ConstantItem(130).AlignRight().Height(24).Image(Logo.Value).FitArea();
                    });
                    columna.Item().PaddingTop(4).LineHorizontal(2).LineColor(ColorMarcaRojo);
                    columna.Item().PaddingTop(4).Text("Generado el " + fechaGeneracion).FontColor(Colors.Grey.Darken2);
                    foreach (var criterio in criterios)
                    {
                        columna.Item().Text(criterio).FontColor(Colors.Grey.Darken2);
                    }
                });

                pagina.Content().PaddingVertical(10).Column(columna =>
                {
                    foreach (var tabla in tablas)
                    {
                        columna.Item().PaddingTop(12).PaddingBottom(4).Text(tabla.Titulo).FontSize(12).Bold().FontColor(ColorMarcaNegro);
                        if (tabla.Filas.Count == 0)
                        {
                            columna.Item().Text("Sin datos para los criterios seleccionados.");
                            continue;
                        }

                        columna.Item().Table(tablaPdf =>
                        {
                            tablaPdf.ColumnsDefinition(columnas =>
                            {
                                foreach (var _ in tabla.Columnas)
                                {
                                    columnas.RelativeColumn();
                                }
                            });

                            tablaPdf.Header(encabezado =>
                            {
                                foreach (var nombreColumna in tabla.Columnas)
                                {
                                    encabezado.Cell().Background(ColorMarcaNegro).Padding(4).Text(nombreColumna).Bold().FontColor(Colors.White);
                                }
                            });

                            foreach (var fila in tabla.Filas)
                            {
                                foreach (var celda in fila)
                                {
                                    tablaPdf.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(celda);
                                }
                            }
                        });
                    }
                });

                pagina.Footer().Row(fila =>
                {
                    fila.RelativeItem().Text("SOTRA · CVM").FontColor(Colors.Grey.Darken1);
                    fila.RelativeItem().AlignRight().Text(texto =>
                    {
                        texto.Span("Página ");
                        texto.CurrentPageNumber();
                        texto.Span(" de ");
                        texto.TotalPages();
                    });
                });
            });
        });

        return documento;
    }
}

public static class HoraUruguay
{
    private static readonly TimeZoneInfo ZonaHoraria = TimeZoneInfo.FindSystemTimeZoneById("America/Montevideo");

    public static DateTime Convertir(DateTime fechaUtc)
    {
        var fechaConZona = DateTime.SpecifyKind(fechaUtc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(fechaConZona, ZonaHoraria);
    }
}
