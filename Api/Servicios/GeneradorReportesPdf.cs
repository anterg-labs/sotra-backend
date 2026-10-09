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
    public static byte[] Generar(string titulo, List<string> criterios, List<TablaReporte> tablas)
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
                    columna.Item().Text("SOTRA - " + titulo).FontSize(16).Bold();
                    columna.Item().Text("Generado el " + fechaGeneracion);
                    foreach (var criterio in criterios)
                    {
                        columna.Item().Text(criterio);
                    }
                });

                pagina.Content().PaddingVertical(10).Column(columna =>
                {
                    foreach (var tabla in tablas)
                    {
                        columna.Item().PaddingTop(12).PaddingBottom(4).Text(tabla.Titulo).FontSize(12).Bold();
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
                                    encabezado.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(nombreColumna).Bold();
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

                pagina.Footer().AlignCenter().Text(texto =>
                {
                    texto.Span("Página ");
                    texto.CurrentPageNumber();
                    texto.Span(" de ");
                    texto.TotalPages();
                });
            });
        });

        return documento.GeneratePdf();
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
