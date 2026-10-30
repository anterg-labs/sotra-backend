using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NUnit.Framework;

namespace Api.Pruebas.Caracterizacion;

/// <summary>Datos que alimentan los reportes: fecha prevista, tiempo estimado, causa de falla, uso, costos y motivos.</summary>
public class DatosParaReportesPruebas : PruebaBase
{
    private const long RolTecnico = 3;

    private async Task<JsonElement> CrearOrdenAsync(JsonElement activo, string tipo, object? extras = null,
        DateOnly? fechaPrevista = null)
    {
        var cuerpo = new Dictionary<string, object?>
        {
            ["clienteId"] = activo.GetProperty("clienteId").GetInt64(),
            ["activoId"] = activo.GetProperty("id").GetInt64(),
            ["tipo"] = tipo,
            ["prioridad"] = "media",
            ["descripcion"] = "Orden para datos de reportes",
            ["fechaPrevistaEjecucion"] = fechaPrevista ?? DateOnly.FromDateTime(DateTime.UtcNow),
            ["repuestos"] = Array.Empty<object>(),
        };
        if (extras is not null)
        {
            foreach (var propiedad in extras.GetType().GetProperties())
            {
                cuerpo[propiedad.Name] = propiedad.GetValue(extras);
            }
        }

        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/ordenes-trabajo", cuerpo);
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created), await respuesta.Content.ReadAsStringAsync());
        return await respuesta.LeerJsonAsync();
    }

    private async Task<string> CodigoDeErrorAsync(HttpResponseMessage respuesta, HttpStatusCode estado)
    {
        Assert.That(respuesta.StatusCode, Is.EqualTo(estado));
        return (await respuesta.LeerJsonAsync()).GetProperty("codigo").GetString()!;
    }

    [Test]
    public async Task LaFechaPrevistaEsUnaColumnaPropiaYNoEnsuciaElHistorial()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var prevista = new DateOnly(2027, 3, 15);
        var orden = await CrearOrdenAsync(activo, "preventivo", fechaPrevista: prevista);
        var id = orden.GetProperty("id").GetInt64();

        Assert.That(orden.GetProperty("fechaPrevistaEjecucion").GetString(), Is.EqualTo("2027-03-15"));
        var detalle = await (await Cliente.GetAsync($"/api/v1/ordenes-trabajo/{id}")).LeerJsonAsync();
        Assert.That(detalle.GetProperty("historialEstados")[0].GetProperty("observacion").ValueKind, Is.EqualTo(JsonValueKind.Null));

        var cambio = await Cliente.PutAsJsonAsync($"/api/v1/ordenes-trabajo/{id}", new
        {
            clienteId = activo.GetProperty("clienteId").GetInt64(),
            activoId = activo.GetProperty("id").GetInt64(),
            tecnicoId = (long?)null,
            tipo = "preventivo",
            prioridad = "media",
            descripcion = "Orden para datos de reportes",
            observaciones = "",
            fechaPrevistaEjecucion = new DateOnly(2027, 4, 2),
            estado = "pendiente",
            sugerencias = Array.Empty<string>(),
            repuestos = Array.Empty<object>(),
        });
        Assert.That(cambio.StatusCode, Is.EqualTo(HttpStatusCode.OK), await cambio.Content.ReadAsStringAsync());
        Assert.That((await cambio.LeerJsonAsync()).GetProperty("fechaPrevistaEjecucion").GetString(), Is.EqualTo("2027-04-02"));
    }

    [Test]
    public async Task ElTiempoEstimadoSeGuardaYUnValorFueraDeRangoSeRechaza()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var orden = await CrearOrdenAsync(activo, "correctivo", new { tiempoEstimadoHoras = 3.5m });
        Assert.That(orden.GetProperty("tiempoEstimadoHoras").GetDecimal(), Is.EqualTo(3.5m));

        var invalido = await Cliente.PostAsJsonAsync("/api/v1/ordenes-trabajo", new
        {
            clienteId = activo.GetProperty("clienteId").GetInt64(),
            activoId = activo.GetProperty("id").GetInt64(),
            tipo = "correctivo",
            prioridad = "media",
            descripcion = "Estimado inválido",
            fechaPrevistaEjecucion = DateOnly.FromDateTime(DateTime.UtcNow),
            tiempoEstimadoHoras = 0,
            repuestos = Array.Empty<object>(),
        });
        Assert.That(invalido.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task ElCatalogoDeCausasDeFallaSeListaYSePuedeAdministrar()
    {
        var lista = await (await Cliente.GetAsync("/api/v1/causas-falla?estado=activo")).LeerJsonAsync();
        Assert.That(lista.GetArrayLength(), Is.GreaterThanOrEqualTo(8));

        var nombre = "Causa " + DatosDePrueba.Sufijo();
        var creada = await Cliente.PostAsJsonAsync("/api/v1/causas-falla", new { nombre });
        Assert.That(creada.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var id = (await creada.LeerJsonAsync()).GetProperty("id").GetInt64();

        var duplicada = await Cliente.PostAsJsonAsync("/api/v1/causas-falla", new { nombre = nombre.ToUpperInvariant() });
        Assert.That(await CodigoDeErrorAsync(duplicada, HttpStatusCode.Conflict), Is.EqualTo("CAUSA_FALLA_DUPLICADA"));

        var inactiva = await Cliente.PatchAsJsonAsync($"/api/v1/causas-falla/{id}/estado", new { estado = "inactivo" });
        Assert.That((await inactiva.LeerJsonAsync()).GetProperty("estado").GetString(), Is.EqualTo("inactivo"));
        var soloActivas = await (await Cliente.GetAsync("/api/v1/causas-falla?estado=activo")).LeerJsonAsync();
        Assert.That(soloActivas.EnumerateArray().Any(causa => causa.GetProperty("id").GetInt64() == id), Is.False);

        var invalido = await Cliente.PatchAsJsonAsync($"/api/v1/causas-falla/{id}/estado", new { estado = "borrado" });
        Assert.That(invalido.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await Cliente.PatchAsJsonAsync("/api/v1/causas-falla/999999/estado", new { estado = "activo" })).StatusCode,
            Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task LaCausaDeFallaSoloAplicaAUnaCorrectivaYDebeEstarActiva()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var causas = await (await Cliente.GetAsync("/api/v1/causas-falla?estado=activo")).LeerJsonAsync();
        var causaId = causas[0].GetProperty("id").GetInt64();

        var correctiva = await CrearOrdenAsync(activo, "correctivo", new { causaFallaId = causaId });
        Assert.That(correctiva.GetProperty("causaFallaId").GetInt64(), Is.EqualTo(causaId));
        Assert.That(correctiva.GetProperty("causaFalla").GetString(), Is.EqualTo(causas[0].GetProperty("nombre").GetString()));

        string Cuerpo(string tipo, long causa) => JsonSerializer.Serialize(new
        {
            clienteId = activo.GetProperty("clienteId").GetInt64(),
            activoId = activo.GetProperty("id").GetInt64(),
            tipo,
            prioridad = "media",
            descripcion = "Causa de falla",
            fechaPrevistaEjecucion = DateOnly.FromDateTime(DateTime.UtcNow),
            causaFallaId = causa,
            repuestos = Array.Empty<object>(),
        });

        var preventiva = await Cliente.PostAsync("/api/v1/ordenes-trabajo",
            new StringContent(Cuerpo("preventivo", causaId), System.Text.Encoding.UTF8, "application/json"));
        Assert.That(await CodigoDeErrorAsync(preventiva, HttpStatusCode.BadRequest), Is.EqualTo("CAUSA_FALLA_SOLO_CORRECTIVA"));

        var inexistente = await Cliente.PostAsync("/api/v1/ordenes-trabajo",
            new StringContent(Cuerpo("correctivo", 999999), System.Text.Encoding.UTF8, "application/json"));
        Assert.That(await CodigoDeErrorAsync(inexistente, HttpStatusCode.BadRequest), Is.EqualTo("CAUSA_FALLA_INVALIDA"));
    }

    [Test]
    public async Task LaLecturaDeUsoQuedaRegistradaConCadaCambioYSeValidaContraLasAnteriores()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        Assert.That(activo.GetProperty("unidadUso").GetString(), Is.EqualTo("horas"));

        var primera = await CrearOrdenAsync(activo, "correctivo", new { lecturaUso = 100m });
        var id = primera.GetProperty("id").GetInt64();
        Assert.That(primera.GetProperty("lecturaUso").GetDecimal(), Is.EqualTo(100m));
        Assert.That(primera.GetProperty("unidadUso").GetString(), Is.EqualTo("horas"));
        var historial = primera.GetProperty("historialLecturaUso");
        Assert.That(historial.GetArrayLength(), Is.EqualTo(1));
        Assert.That(historial[0].GetProperty("lecturaAnterior").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(historial[0].GetProperty("lecturaNueva").GetDecimal(), Is.EqualTo(100m));

        var corregida = await Cliente.PutAsJsonAsync($"/api/v1/ordenes-trabajo/{id}", new
        {
            clienteId = activo.GetProperty("clienteId").GetInt64(),
            activoId = activo.GetProperty("id").GetInt64(),
            tecnicoId = (long?)null,
            tipo = "correctivo",
            prioridad = "media",
            descripcion = "Orden para datos de reportes",
            observaciones = "",
            fechaPrevistaEjecucion = DateOnly.FromDateTime(DateTime.UtcNow),
            estado = "pendiente",
            lecturaUso = 120m,
            sugerencias = Array.Empty<string>(),
            repuestos = Array.Empty<object>(),
        });
        Assert.That(corregida.StatusCode, Is.EqualTo(HttpStatusCode.OK), await corregida.Content.ReadAsStringAsync());
        var cambios = (await corregida.LeerJsonAsync()).GetProperty("historialLecturaUso");
        Assert.That(cambios.GetArrayLength(), Is.EqualTo(2));
        Assert.That(cambios[1].GetProperty("lecturaAnterior").GetDecimal(), Is.EqualTo(100m));
        Assert.That(cambios[1].GetProperty("lecturaNueva").GetDecimal(), Is.EqualTo(120m));

        var menor = await Cliente.PostAsJsonAsync("/api/v1/ordenes-trabajo", new
        {
            clienteId = activo.GetProperty("clienteId").GetInt64(),
            activoId = activo.GetProperty("id").GetInt64(),
            tipo = "correctivo",
            prioridad = "media",
            descripcion = "Lectura menor que la anterior",
            fechaPrevistaEjecucion = DateOnly.FromDateTime(DateTime.UtcNow),
            lecturaUso = 90,
            repuestos = Array.Empty<object>(),
        });
        Assert.That(await CodigoDeErrorAsync(menor, HttpStatusCode.BadRequest), Is.EqualTo("LECTURA_USO_INVALIDA"));
    }

    [Test]
    public async Task ElTecnicoPuedeInformarLaLecturaAlCambiarElEstadoYLaPausaLlevaUnMotivoCodificado()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var orden = await CrearOrdenAsync(activo, "correctivo");
        var id = orden.GetProperty("id").GetInt64();
        var tecnico = await DatosDePrueba.CrearUsuarioAsync(Cliente, DatosDePrueba.UsuarioNuevo(idsRol: [RolTecnico]));
        (await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{id}/tecnico", new { tecnicoId = tecnico.GetProperty("id").GetInt64() }))
            .EnsureSuccessStatusCode();

        var enCurso = await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{id}/estado", new { estado = "en_progreso", lecturaUso = 250.5m });
        Assert.That(enCurso.StatusCode, Is.EqualTo(HttpStatusCode.OK), await enCurso.Content.ReadAsStringAsync());
        Assert.That((await enCurso.LeerJsonAsync()).GetProperty("lecturaUso").GetDecimal(), Is.EqualTo(250.5m));

        var motivos = await (await Cliente.GetAsync("/api/v1/ordenes-trabajo/motivos")).LeerJsonAsync();
        Assert.That(motivos.EnumerateArray().Select(motivo => motivo.GetProperty("codigo").GetString()),
            Is.SupersetOf(new[] { "falta_repuesto", "esperando_autorizacion", "esperando_cliente", "otro" }));

        var invalido = await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{id}/estado",
            new { estado = "pausado", motivo = "Sin material", motivoCodigo = "no_existe" });
        Assert.That(await CodigoDeErrorAsync(invalido, HttpStatusCode.BadRequest), Is.EqualTo("MOTIVO_INVALIDO"));

        var pausada = await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{id}/estado",
            new { estado = "pausado", motivo = "Sin material", motivoCodigo = "falta_repuesto" });
        Assert.That(pausada.StatusCode, Is.EqualTo(HttpStatusCode.OK), await pausada.Content.ReadAsStringAsync());
    }

    [Test]
    public async Task LaUnidadDeUsoDelActivoSoloAdmiteHorasOKilometros()
    {
        var (clienteId, modeloId) = await DatosDePrueba.CrearClienteYModeloAsync(Cliente);
        var camion = await Cliente.PostAsJsonAsync("/api/v1/activos", new
        {
            clienteId,
            numSerie = "SN-" + DatosDePrueba.Sufijo(),
            modeloMaquinaId = modeloId,
            unidadUso = "kilometros",
        });
        Assert.That(camion.StatusCode, Is.EqualTo(HttpStatusCode.Created), await camion.Content.ReadAsStringAsync());
        Assert.That((await camion.LeerJsonAsync()).GetProperty("unidadUso").GetString(), Is.EqualTo("kilometros"));

        var invalida = await Cliente.PostAsJsonAsync("/api/v1/activos", new
        {
            clienteId,
            numSerie = "SN-" + DatosDePrueba.Sufijo(),
            modeloMaquinaId = modeloId,
            unidadUso = "millas",
        });
        Assert.That(invalida.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task LosCostosOpcionalesSeGuardanYNoAdmitenValoresNegativos()
    {
        var tipo = await Cliente.PostAsJsonAsync("/api/v1/tipos-maquina",
            new { nombre = "Tipo con costo " + DatosDePrueba.Sufijo(), costoHoraParada = 150.75m });
        Assert.That(tipo.StatusCode, Is.EqualTo(HttpStatusCode.Created), await tipo.Content.ReadAsStringAsync());
        Assert.That((await tipo.LeerJsonAsync()).GetProperty("costoHoraParada").GetDecimal(), Is.EqualTo(150.75m));

        var negativo = await Cliente.PostAsJsonAsync("/api/v1/tipos-maquina",
            new { nombre = "Tipo negativo " + DatosDePrueba.Sufijo(), costoHoraParada = -1 });
        Assert.That(negativo.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var repuesto = await Cliente.PostAsJsonAsync("/api/v1/repuestos", new
        {
            codigo = "REP-" + DatosDePrueba.Sufijo(),
            nombre = "Repuesto con costo " + DatosDePrueba.Sufijo(),
            cantidad = 5,
            cantidadMinima = 1,
            costoUnitario = 12.5m,
        });
        Assert.That(repuesto.StatusCode, Is.EqualTo(HttpStatusCode.Created), await repuesto.Content.ReadAsStringAsync());
        Assert.That((await repuesto.LeerJsonAsync()).GetProperty("costoUnitario").GetDecimal(), Is.EqualTo(12.5m));
    }
}
