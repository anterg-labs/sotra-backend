using System.Net;
using System.Net.Http.Json;
using Api.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using SOTRA_BACKEND.Contexto;

namespace Api.Pruebas.Caracterizacion;

public sealed class RegistroErroresPruebas : PruebaBase
{
    [Test]
    public async Task GuardaFalloDeSesionEnLaBaseSinDatosLibres()
    {
        var correlacion = Guid.NewGuid().ToString("N");
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, "/api/v1/registro-errores/sesion")
        {
            Content = JsonContent.Create(new { codigo = "SESION_API_NO_DISPONIBLE", estadoHttp = 503 }),
        };
        solicitud.Headers.Add(CorrelacionMiddleware.Cabecera, correlacion);
        var respuesta = await Cliente.SendAsync(solicitud);
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        using var alcance = EntornoDePruebas.Fabrica.Services.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<SotraContext>();
        var cantidad = await contexto.Database.SqlQueryRaw<long>(
            "SELECT count(*)::bigint AS \"Value\" FROM operativo.registro_error WHERE id_correlacion = {0} AND origen = 'frontend' AND codigo = 'SESION_API_NO_DISPONIBLE'",
            correlacion).SingleAsync();
        Assert.That(cantidad, Is.EqualTo(1));
    }

    [Test]
    public async Task RechazaCodigosNoPermitidos()
    {
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/registro-errores/sesion",
            new { codigo = "contraseña=secreta", estadoHttp = 500 });
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task LogErrorDelServidorSeGuardaSinMensajeLibre()
    {
        var correlacion = Guid.NewGuid().ToString("N");
        using var alcance = EntornoDePruebas.Fabrica.Services.CreateScope();
        var registro = alcance.ServiceProvider.GetRequiredService<ILogger<RegistroErroresPruebas>>();
        using (Serilog.Context.LogContext.PushProperty("IdCorrelacion", correlacion))
            registro.LogError("Dato privado de prueba: {Dato}", "NO_GUARDAR_ESTE_TEXTO");

        var contexto = alcance.ServiceProvider.GetRequiredService<SotraContext>();
        // El guardado ocurre en segundo plano: se espera hasta que el registro aparezca.
        long cantidad = 0;
        for (var intento = 0; intento < 50 && cantidad == 0; intento++)
        {
            cantidad = await contexto.Database.SqlQueryRaw<long>(
                "SELECT count(*)::bigint AS \"Value\" FROM operativo.registro_error WHERE id_correlacion = {0} AND origen = 'api' AND codigo = 'LOG_ERROR'",
                correlacion).SingleAsync();
            if (cantidad == 0) await Task.Delay(100);
        }
        Assert.That(cantidad, Is.EqualTo(1));
        var sensible = await contexto.Database.SqlQueryRaw<long>(
            "SELECT count(*)::bigint AS \"Value\" FROM operativo.registro_error WHERE id_correlacion = {0} AND (categoria LIKE '%NO_GUARDAR%' OR ruta LIKE '%NO_GUARDAR%')",
            correlacion).SingleAsync();
        Assert.That(sensible, Is.Zero);
    }
}
