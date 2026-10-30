using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;
using SOTRA_BACKEND.Operaciones;

namespace Api.Pruebas.Caracterizacion;

// RFG-05: avisos por asignación, cambio de estado y solicitud de revisión. Las pruebas corren sin sesión, así que quien
// provoca el cambio no es nadie y los avisos llegan a todos los destinatarios; la bandeja de cada persona se prueba sobre
// el acceso a datos porque el endpoint necesita una sesión de Keycloak.
public class NotificacionesPruebas : PruebaBase
{
    private const long RolTecnico = 3;

    private async Task<long> CrearTecnicoAsync()
    {
        var tecnico = await DatosDePrueba.CrearUsuarioAsync(Cliente, DatosDePrueba.UsuarioNuevo(idsRol: [RolTecnico]));
        return tecnico.GetProperty("id").GetInt64();
    }

    private async Task<long> CrearOrdenAsync(long? tecnicoId)
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/ordenes-trabajo", new
        {
            clienteId = activo.GetProperty("clienteId").GetInt64(),
            activoId = activo.GetProperty("id").GetInt64(),
            tecnicoId,
            tipo = "correctivo",
            prioridad = "media",
            descripcion = "Revisión de prueba de notificaciones",
            observaciones = "",
            fechaPrevistaEjecucion = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            sugerencias = Array.Empty<string>(),
            repuestos = Array.Empty<object>(),
        });
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt64();
    }

    private async Task CambiarEstadoAsync(long ordenId, string estado)
    {
        var respuesta = await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{ordenId}/estado", new { estado, motivo = (string?)null });
        respuesta.EnsureSuccessStatusCode();
    }

    private static async Task<List<Notificacion>> LeerAsync(long ordenId)
    {
        using var alcance = EntornoDePruebas.Fabrica.Services.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<SotraContext>();
        return await contexto.Notificaciones.AsNoTracking().Where(n => n.IdOrden == ordenId).OrderBy(n => n.IdNotificacion).ToListAsync();
    }

    [Test]
    public async Task AsignarUnTecnicoAlCrearLaOrdenLeAvisaSoloAEl()
    {
        var tecnicoId = await CrearTecnicoAsync();

        var ordenId = await CrearOrdenAsync(tecnicoId);

        var avisos = await LeerAsync(ordenId);
        Assert.That(avisos, Has.Count.EqualTo(1));
        Assert.That(avisos[0].IdUsuario, Is.EqualTo(tecnicoId));
        Assert.That(avisos[0].Tipo, Is.EqualTo(TiposDeNotificacion.OrdenAsignada));
        Assert.That(avisos[0].Mensaje, Does.Contain("#" + ordenId));
        Assert.That(avisos[0].LeidaEn, Is.Null);
    }

    [Test]
    public async Task UnaOrdenSinTecnicoNoAvisaANadieAlCrearse()
    {
        var ordenId = await CrearOrdenAsync(null);

        Assert.That(await LeerAsync(ordenId), Is.Empty);
    }

    [Test]
    public async Task AsignarElTecnicoDespuesGeneraUnSoloAviso()
    {
        var tecnicoId = await CrearTecnicoAsync();
        var ordenId = await CrearOrdenAsync(null);

        var respuesta = await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{ordenId}/tecnico", new { tecnicoId });
        respuesta.EnsureSuccessStatusCode();

        var avisos = await LeerAsync(ordenId);
        Assert.That(avisos, Has.Count.EqualTo(1), "una asignación no debe repetir el aviso por el posible cambio de estado");
        Assert.That(avisos[0].IdUsuario, Is.EqualTo(tecnicoId));
    }

    [Test]
    public async Task UnCambioDeEstadoLeAvisaAlTecnicoYLaRevisionALosJefes()
    {
        var tecnicoId = await CrearTecnicoAsync();
        var ordenId = await CrearOrdenAsync(tecnicoId);

        await CambiarEstadoAsync(ordenId, "en_progreso");
        await CambiarEstadoAsync(ordenId, "en_revision");

        var avisos = await LeerAsync(ordenId);
        var alTecnico = avisos.Where(a => a.IdUsuario == tecnicoId).Select(a => a.Tipo).ToList();
        Assert.That(alTecnico, Is.EqualTo(new[] { TiposDeNotificacion.OrdenAsignada, TiposDeNotificacion.OrdenCambioEstado }));
        Assert.That(avisos.First(a => a.Tipo == TiposDeNotificacion.OrdenCambioEstado).Mensaje, Does.Contain("En progreso"));

        var revision = avisos.Where(a => a.Tipo == TiposDeNotificacion.OrdenEnRevision).ToList();
        Assert.That(revision, Is.Not.Empty, "la solicitud de revisión llega a los jefes de mantenimiento");
        Assert.That(revision.Select(a => a.IdUsuario), Does.Not.Contain(tecnicoId));
        Assert.That(revision.Select(a => a.IdUsuario), Is.Unique);
    }

    [Test]
    public async Task CadaPersonaVeYMarcaSoloSusAvisos()
    {
        var dueno = await CrearTecnicoAsync();
        var otro = await CrearTecnicoAsync();
        using var alcance = EntornoDePruebas.Fabrica.Services.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<SotraContext>();
        var dao = new NotificacionDAO(contexto);
        foreach (var mensaje in new[] { "Primero", "Segundo", "Tercero" })
        {
            contexto.Notificaciones.Add(new Notificacion
            {
                IdUsuario = dueno,
                Tipo = TiposDeNotificacion.OrdenAsignada,
                Mensaje = mensaje,
                CreadaEn = DateTime.UtcNow,
            });
        }

        await contexto.SaveChangesAsync();
        var primera = await contexto.Notificaciones.Where(n => n.IdUsuario == dueno).OrderBy(n => n.IdNotificacion).FirstAsync();

        Assert.That(await dao.ContarNoLeidasAsync(dueno, default), Is.EqualTo(3));
        Assert.That(await dao.ContarNoLeidasAsync(otro, default), Is.Zero);
        var (pagina, total) = await dao.ListarPaginaAsync(dueno, false, 1, 2, default);
        Assert.That(total, Is.EqualTo(3));
        Assert.That(pagina.Select(n => n.Mensaje), Is.EqualTo(new[] { "Tercero", "Segundo" }), "las más nuevas primero");

        Assert.That(await dao.MarcarLeidaAsync(otro, primera.IdNotificacion, default), Is.False, "no se puede marcar un aviso ajeno");
        Assert.That(await dao.ContarNoLeidasAsync(dueno, default), Is.EqualTo(3));
        Assert.That(await dao.MarcarLeidaAsync(dueno, primera.IdNotificacion, default), Is.True);
        Assert.That(await dao.MarcarLeidaAsync(dueno, primera.IdNotificacion, default), Is.True, "es idempotente");
        Assert.That(await dao.ContarNoLeidasAsync(dueno, default), Is.EqualTo(2));
        Assert.That((await dao.ListarPaginaAsync(dueno, true, 1, 20, default)).Total, Is.EqualTo(2));

        await dao.MarcarTodasLeidasAsync(dueno, default);
        Assert.That(await dao.ContarNoLeidasAsync(dueno, default), Is.Zero);
    }

    [Test]
    public async Task SinSesionLaBandejaEstaVaciaYNoSePuedeMarcarNada()
    {
        var lista = await (await Cliente.GetAsync("/api/v1/notificaciones")).LeerJsonAsync();
        var noLeidas = await (await Cliente.GetAsync("/api/v1/notificaciones/no-leidas")).LeerJsonAsync();

        Assert.That(lista.GetProperty("totalElementos").GetInt32(), Is.Zero);
        Assert.That(noLeidas.GetProperty("total").GetInt32(), Is.Zero);
        Assert.That((await Cliente.PatchAsync("/api/v1/notificaciones/1/leida", null)).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await Cliente.PostAsync("/api/v1/notificaciones/marcar-leidas", null)).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task LaPaginacionInvalidaSeRechazaConProblemDetailsYCodigoEstable()
    {
        var respuesta = await Cliente.GetAsync("/api/v1/notificaciones?tamanoPagina=101");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var cuerpo = await respuesta.LeerJsonAsync();
        Assert.That(cuerpo.GetProperty("codigo").GetString(), Is.EqualTo("PAGINACION_INVALIDA"));
        Assert.That(cuerpo.GetProperty("status").GetInt32(), Is.EqualTo(400));
        Assert.That(cuerpo.TryGetProperty("idCorrelacion", out _), Is.True);
    }
}
