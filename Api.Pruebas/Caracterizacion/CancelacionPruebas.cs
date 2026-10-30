using System.Net.Http.Json;
using NUnit.Framework;

namespace Api.Pruebas.Caracterizacion;

/// <summary>
/// Si la persona cierra la conexión a mitad de una escritura, la transacción se deshace entera: el stock nunca
/// cambia sin dejar su movimiento, y el servidor sigue respondiendo.
/// </summary>
public class CancelacionPruebas : PruebaBase
{
    private const int StockInicial = 100;
    private const int UnidadesPorIngreso = 3;
    private const int Solicitudes = 40;

    [Test]
    public async Task UnaSolicitudCanceladaPorElClienteNoDejaElStockAMedias()
    {
        var respuestaAlta = await Cliente.PostAsJsonAsync("/api/v1/repuestos", new
        {
            codigo = "CAN-" + DatosDePrueba.Sufijo(),
            nombre = "Repuesto de cancelación " + DatosDePrueba.Sufijo(),
            ubicacion = "Depósito A",
            cantidad = StockInicial,
            cantidadMinima = 1,
        });
        respuestaAlta.EnsureSuccessStatusCode();
        var id = (await respuestaAlta.LeerJsonAsync()).GetProperty("id").GetInt64();

        var cuerpo = new
        {
            cantidad = UnidadesPorIngreso,
            fecha = DateOnly.FromDateTime(DateTime.UtcNow),
            motivo = "Compra",
            ubicacion = "Depósito A",
        };

        var tareas = Enumerable.Range(0, Solicitudes).Select(async indice =>
        {
            using var cancelacion = new CancellationTokenSource();
            cancelacion.CancelAfter(TimeSpan.FromMilliseconds(indice % 12));
            try
            {
                using var respuesta = await Cliente.PostAsJsonAsync($"/api/v1/repuestos/{id}/ingresos", cuerpo, cancelacion.Token);
                return (int)respuesta.StatusCode;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
        });
        var resultados = await Task.WhenAll(tareas);

        var repuesto = await (await Cliente.GetAsync($"/api/v1/repuestos/{id}")).LeerJsonAsync();
        var movimientos = await (await Cliente.GetAsync($"/api/v1/movimientos-stock?repuestoId={id}&tamanoPagina=100"))
            .LeerJsonAsync();
        var registrados = movimientos.GetProperty("totalElementos").GetInt32();

        Assert.That(resultados.Where(codigo => codigo != 0), Has.All.EqualTo(201), "Ninguna solicitud completa responde un error");
        Assert.That(repuesto.GetProperty("cantidad").GetInt32(), Is.EqualTo(StockInicial + registrados * UnidadesPorIngreso),
            "El stock coincide exactamente con los movimientos registrados");
        Assert.That((await Cliente.GetAsync("/api/v1/version")).IsSuccessStatusCode, Is.True, "El servidor sigue respondiendo");
    }
}
