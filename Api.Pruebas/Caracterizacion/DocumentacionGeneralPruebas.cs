using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NUnit.Framework;

namespace Api.Pruebas.Caracterizacion;

/// <summary>Datos generales de la documentación: título, tipo, descripción, vigencia y modelo de máquina (RF006-01).</summary>
public class DocumentacionGeneralPruebas : PruebaBase
{
    private static readonly byte[] Pdf = "%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF"u8.ToArray();

    private Task<HttpResponseMessage> SubirAsync(string nombre, Dictionary<string, string>? datos = null)
    {
        var formulario = new MultipartFormDataContent { { new StringContent("false"), "usaSugerenciasIA" } };
        foreach (var (campo, valor) in datos ?? [])
        {
            formulario.Add(new StringContent(valor), campo);
        }

        var archivo = new ByteArrayContent(Pdf);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        formulario.Add(archivo, "archivo", nombre);
        return Cliente.PostAsync("/api/v1/documentos-tecnicos", formulario);
    }

    private async Task<JsonElement> ListarAsync(string consulta) =>
        await (await Cliente.GetAsync("/api/v1/documentos-tecnicos?" + consulta)).LeerJsonAsync();

    private static async Task<string?> CodigoAsync(HttpResponseMessage respuesta) =>
        (await respuesta.LeerJsonAsync()).GetProperty("codigo").GetString();

    [Test]
    public async Task SinDatosGeneralesElTituloEsElNombreDelArchivoYElTipoManualTecnicoVigente()
    {
        var nombre = "sin-datos-" + DatosDePrueba.Sufijo() + ".pdf";

        var respuesta = await SubirAsync(nombre);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var documento = await respuesta.LeerJsonAsync();
        Assert.That(documento.GetProperty("titulo").GetString(), Is.EqualTo(nombre));
        Assert.That(documento.GetProperty("tipo").GetString(), Is.EqualTo("manual_tecnico"));
        Assert.That(documento.GetProperty("vigente").GetBoolean(), Is.True);
        Assert.That(documento.GetProperty("descripcion").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(documento.GetProperty("modeloMaquinaId").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task ElAltaGuardaTituloTipoDescripcionYModeloYLaRespuestaLosDevuelve()
    {
        var tipo = await DatosDePrueba.CrearTipoMaquinaAsync(Cliente);
        var modelo = await DatosDePrueba.CrearModeloMaquinaAsync(Cliente, tipo.GetProperty("id").GetInt64());
        var modeloId = modelo.GetProperty("id").GetInt64();

        var respuesta = await SubirAsync("norma-" + DatosDePrueba.Sufijo() + ".pdf", new()
        {
            ["titulo"] = "  Norma de seguridad  ",
            ["tipo"] = "normativa",
            ["descripcion"] = "Uso de EPP en el taller",
            ["modeloMaquinaId"] = modeloId.ToString(),
        });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var documento = await respuesta.LeerJsonAsync();
        Assert.That(documento.GetProperty("titulo").GetString(), Is.EqualTo("Norma de seguridad"));
        Assert.That(documento.GetProperty("tipo").GetString(), Is.EqualTo("normativa"));
        Assert.That(documento.GetProperty("descripcion").GetString(), Is.EqualTo("Uso de EPP en el taller"));
        Assert.That(documento.GetProperty("modeloMaquinaId").GetInt64(), Is.EqualTo(modeloId));
        Assert.That(documento.GetProperty("nombreModelo").GetString(), Is.EqualTo(modelo.GetProperty("nombre").GetString()));
    }

    [Test]
    public async Task UnTipoFueraDelCatalogoSeRechazaConCodigoEstable()
    {
        var respuesta = await SubirAsync("tipo-" + DatosDePrueba.Sufijo() + ".pdf", new() { ["tipo"] = "contrato" });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await CodigoAsync(respuesta), Is.EqualTo("TIPO_DOCUMENTO_INVALIDO"));
    }

    [Test]
    public async Task UnTituloOUnaDescripcionDemasiadoLargosSeRechazan()
    {
        var titulo = await SubirAsync("t-" + DatosDePrueba.Sufijo() + ".pdf", new() { ["titulo"] = new string('a', 201) });
        var descripcion = await SubirAsync("d-" + DatosDePrueba.Sufijo() + ".pdf", new() { ["descripcion"] = new string('a', 1001) });

        Assert.That(titulo.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await CodigoAsync(titulo), Is.EqualTo("TITULO_DEMASIADO_LARGO"));
        Assert.That(descripcion.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await CodigoAsync(descripcion), Is.EqualTo("DESCRIPCION_DEMASIADO_LARGA"));
    }

    [Test]
    public async Task UnModeloInexistenteSeRechazaConCodigoEstable()
    {
        var respuesta = await SubirAsync("m-" + DatosDePrueba.Sufijo() + ".pdf", new() { ["modeloMaquinaId"] = long.MaxValue.ToString() });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await CodigoAsync(respuesta), Is.EqualTo("MODELO_NO_EXISTE"));
    }

    [Test]
    public async Task ElListadoFiltraPorTipoYBuscaPorTituloYDescripcion()
    {
        var marca = "doc" + DatosDePrueba.Sufijo();
        await SubirAsync(marca + "-a.pdf", new() { ["titulo"] = marca + " procedimiento", ["tipo"] = "procedimiento_interno" });
        await SubirAsync(marca + "-b.pdf", new() { ["tipo"] = "normativa", ["descripcion"] = "texto " + marca + " libre" });
        await SubirAsync(marca + "-c.pdf");

        var todos = await ListarAsync("buscar=" + marca);
        var normativas = await ListarAsync($"buscar={marca}&tipo=normativa");
        var porTitulo = await ListarAsync($"buscar={marca}%20procedimiento");

        Assert.That(todos.GetProperty("totalElementos").GetInt32(), Is.EqualTo(3));
        Assert.That(normativas.GetProperty("totalElementos").GetInt32(), Is.EqualTo(1));
        Assert.That(normativas.GetProperty("elementos")[0].GetProperty("tipo").GetString(), Is.EqualTo("normativa"));
        Assert.That(porTitulo.GetProperty("totalElementos").GetInt32(), Is.EqualTo(1));
    }

    [Test]
    public async Task ElListadoFiltraPorModeloDeMaquina()
    {
        var tipo = await DatosDePrueba.CrearTipoMaquinaAsync(Cliente);
        var modeloId = (await DatosDePrueba.CrearModeloMaquinaAsync(Cliente, tipo.GetProperty("id").GetInt64())).GetProperty("id").GetInt64();
        var marca = "mod" + DatosDePrueba.Sufijo();
        await SubirAsync(marca + "-con.pdf", new() { ["modeloMaquinaId"] = modeloId.ToString() });
        await SubirAsync(marca + "-sin.pdf");

        var pagina = await ListarAsync($"buscar={marca}&modeloMaquinaId={modeloId}");

        Assert.That(pagina.GetProperty("totalElementos").GetInt32(), Is.EqualTo(1));
        Assert.That(pagina.GetProperty("elementos")[0].GetProperty("nombreArchivo").GetString(), Does.EndWith("-con.pdf"));
    }

    [Test]
    public async Task UnTipoDeFiltroInvalidoResponde400()
    {
        var respuesta = await Cliente.GetAsync("/api/v1/documentos-tecnicos?tipo=contrato");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await CodigoAsync(respuesta), Is.EqualTo("TIPO_DOCUMENTO_INVALIDO"));
    }

    [Test]
    public async Task ElPutCambiaLosDatosGeneralesSinTocarElArchivo()
    {
        var creado = await (await SubirAsync("put-" + DatosDePrueba.Sufijo() + ".pdf")).LeerJsonAsync();
        var id = creado.GetProperty("id").GetInt64();

        var respuesta = await Cliente.PutAsJsonAsync($"/api/v1/documentos-tecnicos/{id}", new
        {
            titulo = "Política de seguridad 2026",
            tipo = "politica_seguridad",
            descripcion = "Vigente desde octubre",
            activoId = (long?)null,
            modeloMaquinaId = (long?)null,
        });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var documento = await respuesta.LeerJsonAsync();
        Assert.That(documento.GetProperty("titulo").GetString(), Is.EqualTo("Política de seguridad 2026"));
        Assert.That(documento.GetProperty("tipo").GetString(), Is.EqualTo("politica_seguridad"));
        Assert.That(documento.GetProperty("nombreArchivo").GetString(), Is.EqualTo(creado.GetProperty("nombreArchivo").GetString()));
        var descarga = await Cliente.GetAsync($"/api/v1/documentos-tecnicos/{id}/archivo");
        Assert.That(await descarga.Content.ReadAsByteArrayAsync(), Is.EqualTo(Pdf));
    }

    [Test]
    public async Task ElPutRechazaUnTipoInvalidoYUnDocumentoInexistenteResponde404()
    {
        var creado = await (await SubirAsync("put2-" + DatosDePrueba.Sufijo() + ".pdf")).LeerJsonAsync();

        var invalido = await Cliente.PutAsJsonAsync($"/api/v1/documentos-tecnicos/{creado.GetProperty("id").GetInt64()}", new
        { titulo = (string?)null, tipo = "contrato", descripcion = (string?)null, activoId = (long?)null, modeloMaquinaId = (long?)null });
        var inexistente = await Cliente.PutAsJsonAsync($"/api/v1/documentos-tecnicos/{long.MaxValue}", new
        { titulo = (string?)null, tipo = "otro", descripcion = (string?)null, activoId = (long?)null, modeloMaquinaId = (long?)null });

        Assert.That(invalido.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await CodigoAsync(invalido), Is.EqualTo("TIPO_DOCUMENTO_INVALIDO"));
        Assert.That(inexistente.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task UnDocumentoSePuedeMarcarObsoletoYVolverAVigente()
    {
        var marca = "vig" + DatosDePrueba.Sufijo();
        var id = (await (await SubirAsync(marca + ".pdf")).LeerJsonAsync()).GetProperty("id").GetInt64();

        var obsoleto = await Cliente.PatchAsJsonAsync($"/api/v1/documentos-tecnicos/{id}/vigencia", new { vigente = false });
        var soloObsoletos = await ListarAsync($"buscar={marca}&vigente=false");
        var soloVigentes = await ListarAsync($"buscar={marca}&vigente=true");
        var vuelve = await Cliente.PatchAsJsonAsync($"/api/v1/documentos-tecnicos/{id}/vigencia", new { vigente = true });

        Assert.That(obsoleto.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await obsoleto.LeerJsonAsync()).GetProperty("vigente").GetBoolean(), Is.False);
        Assert.That(soloObsoletos.GetProperty("totalElementos").GetInt32(), Is.EqualTo(1));
        Assert.That(soloVigentes.GetProperty("totalElementos").GetInt32(), Is.EqualTo(0));
        Assert.That((await vuelve.LeerJsonAsync()).GetProperty("vigente").GetBoolean(), Is.True);
    }

    [Test]
    public async Task LaBusquedaDeLaIaIgnoraLosDocumentosObsoletos()
    {
        var palabra = "zorrinoxyz" + DatosDePrueba.Sufijo().Replace("-", "").ToLowerInvariant();
        await using var baseDatos = Api.Servicios.BaseVectorial.CrearConexion();
        await using var servicio = new Api.Servicios.ServicioSugerencias(null!, null!, null!);

        var documentoVectorialId = Convert.ToInt64(await new Npgsql.NpgsqlCommand(
            "INSERT INTO vectorial.documentos (nombre, huella, version_proceso) VALUES ('prueba', 'h', 'v') RETURNING id",
            await baseDatos.OpenConnectionAsync()).ExecuteScalarAsync());
        await using (var fragmento = baseDatos.CreateCommand(
            "INSERT INTO vectorial.fragmentos (documento_id, posicion, pagina, texto) VALUES ($1, 0, 1, $2)"))
        {
            fragmento.Parameters.AddWithValue(documentoVectorialId);
            fragmento.Parameters.AddWithValue("El procedimiento menciona " + palabra + " con claridad.");
            await fragmento.ExecuteNonQueryAsync();
        }

        await using (var documento = baseDatos.CreateCommand("""
            INSERT INTO vectorial.documento_tecnico
                (nombre_archivo, usa_sugerencias_ia, cantidad_fragmentos, archivo, creado_en, documento_vectorial_id, vigente)
            VALUES ('ia.pdf', true, 1, '\x25504446', now(), $1, true)
            """))
        {
            documento.Parameters.AddWithValue(documentoVectorialId);
            await documento.ExecuteNonQueryAsync();
        }

        var vigente = await servicio.BuscarIdsPorPalabrasAsync(palabra, null, CancellationToken.None);

        await using (var obsoleto = baseDatos.CreateCommand(
            "UPDATE vectorial.documento_tecnico SET vigente = false WHERE documento_vectorial_id = $1"))
        {
            obsoleto.Parameters.AddWithValue(documentoVectorialId);
            await obsoleto.ExecuteNonQueryAsync();
        }

        var obsoletoEncontrado = await servicio.BuscarIdsPorPalabrasAsync(palabra, null, CancellationToken.None);

        Assert.That(vigente, Has.Count.EqualTo(1), "un documento vigente sí aparece en la búsqueda");
        Assert.That(obsoletoEncontrado, Is.Empty, "un documento obsoleto no debe usarse para las sugerencias");
    }

    private async Task<long> CrearActivoAsync(long clienteId, long modeloId)
    {
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/activos", DatosDePrueba.ActivoNuevo(clienteId, modeloId));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt64();
    }

    private static async Task<long> CrearDocumentoConFragmentoAsync(Npgsql.NpgsqlDataSource baseDatos, string texto,
        long? activoId, long? modeloId)
    {
        await using var conexion = await baseDatos.OpenConnectionAsync();
        var documentoVectorialId = Convert.ToInt64(await new Npgsql.NpgsqlCommand(
            "INSERT INTO vectorial.documentos (nombre, huella, version_proceso) VALUES ('alcance', 'h', 'v') RETURNING id",
            conexion).ExecuteScalarAsync());
        long fragmentoId;
        await using (var fragmento = new Npgsql.NpgsqlCommand(
            "INSERT INTO vectorial.fragmentos (documento_id, posicion, pagina, texto) VALUES ($1, 0, 1, $2) RETURNING id", conexion))
        {
            fragmento.Parameters.AddWithValue(documentoVectorialId);
            fragmento.Parameters.AddWithValue(texto);
            fragmentoId = Convert.ToInt64(await fragmento.ExecuteScalarAsync());
        }

        await using var documento = new Npgsql.NpgsqlCommand("""
            INSERT INTO vectorial.documento_tecnico
                (nombre_archivo, usa_sugerencias_ia, cantidad_fragmentos, archivo, creado_en, documento_vectorial_id,
                 vigente, activo_id, modelo_maquina_id)
            VALUES ('alcance.pdf', true, 1, '\x25504446', now(), $1, true, $2, $3)
            """, conexion);
        documento.Parameters.AddWithValue(documentoVectorialId);
        documento.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Bigint, (object?)activoId ?? DBNull.Value);
        documento.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Bigint, (object?)modeloId ?? DBNull.Value);
        await documento.ExecuteNonQueryAsync();
        return fragmentoId;
    }

    [Test]
    public async Task ConActivoLaBusquedaUsaSoloLaDocumentacionDeEsaMaquinaYLaGeneral()
    {
        var palabra = "carpinchoxyz" + DatosDePrueba.Sufijo().Replace("-", "").ToLowerInvariant();
        var texto = "El procedimiento menciona " + palabra + " con claridad.";
        var (clienteId, modeloId) = await DatosDePrueba.CrearClienteYModeloAsync(Cliente);
        var tipo = await DatosDePrueba.CrearTipoMaquinaAsync(Cliente);
        var otroModeloId = (await DatosDePrueba.CrearModeloMaquinaAsync(Cliente, tipo.GetProperty("id").GetInt64()))
            .GetProperty("id").GetInt64();
        var activoId = await CrearActivoAsync(clienteId, modeloId);
        var activoDelMismoModeloId = await CrearActivoAsync(clienteId, modeloId);
        var activoDeOtroModeloId = await CrearActivoAsync(clienteId, otroModeloId);
        await using var baseDatos = Api.Servicios.BaseVectorial.CrearConexion();
        await using var servicio = new Api.Servicios.ServicioSugerencias(null!, null!, null!);

        var delActivo = await CrearDocumentoConFragmentoAsync(baseDatos, texto, activoId, null);
        var deOtroActivoDelModelo = await CrearDocumentoConFragmentoAsync(baseDatos, texto, activoDelMismoModeloId, null);
        var delModelo = await CrearDocumentoConFragmentoAsync(baseDatos, texto, null, modeloId);
        var general = await CrearDocumentoConFragmentoAsync(baseDatos, texto, null, null);
        var deOtraMaquina = await CrearDocumentoConFragmentoAsync(baseDatos, texto, activoDeOtroModeloId, null);
        var deOtroModelo = await CrearDocumentoConFragmentoAsync(baseDatos, texto, null, otroModeloId);

        var conActivo = await servicio.BuscarIdsPorPalabrasAsync(palabra, activoId, CancellationToken.None);
        var sinActivo = await servicio.BuscarIdsPorPalabrasAsync(palabra, null, CancellationToken.None);

        Assert.That(conActivo, Is.EquivalentTo(new[] { delActivo, deOtroActivoDelModelo, delModelo, general }),
            "con activo se usan sus manuales, los de su modelo y los generales");
        Assert.That(sinActivo, Is.EquivalentTo(new[] { delActivo, deOtroActivoDelModelo, delModelo, general, deOtraMaquina, deOtroModelo }),
            "sin activo se busca en toda la documentación vigente");
    }

    [Test]
    public async Task UnFragmentoLejanoSoloPasaSiCoincidePorPalabras()
    {
        await using var baseDatos = Api.Servicios.BaseVectorial.CrearConexion();
        await using var servicio = new Api.Servicios.ServicioSugerencias(null!, null!, null!);
        var documentoVectorialId = Convert.ToInt64(await new Npgsql.NpgsqlCommand(
            "INSERT INTO vectorial.documentos (nombre, huella, version_proceso) VALUES ('lejano', 'h', 'v') RETURNING id",
            await baseDatos.OpenConnectionAsync()).ExecuteScalarAsync());

        // El fragmento y la consulta tienen vectores perpendiculares: distancia coseno 1, mucho más que el máximo.
        var vectorDelFragmento = new float[Api.Servicios.ServicioEmbeddings.DimensionesDelEmbedding];
        vectorDelFragmento[0] = 1;
        var vectorDeLaConsulta = new float[Api.Servicios.ServicioEmbeddings.DimensionesDelEmbedding];
        vectorDeLaConsulta[1] = 1;
        long fragmentoId;
        await using (var fragmento = baseDatos.CreateCommand(
            "INSERT INTO vectorial.fragmentos (documento_id, posicion, pagina, texto, embedding) VALUES ($1, 0, 1, 'Engrase del brazo', $2) RETURNING id"))
        {
            fragmento.Parameters.AddWithValue(documentoVectorialId);
            fragmento.Parameters.AddWithValue(new Pgvector.Vector(vectorDelFragmento));
            fragmentoId = Convert.ToInt64(await fragmento.ExecuteScalarAsync());
        }

        var consulta = new Pgvector.Vector(vectorDeLaConsulta);
        var soloPorParecido = await servicio.LeerFragmentosCercanosAsync([fragmentoId], [], consulta, CancellationToken.None);
        var tambienPorPalabras = await servicio.LeerFragmentosCercanosAsync([fragmentoId], [fragmentoId], consulta, CancellationToken.None);

        Assert.That(soloPorParecido, Is.Empty, "por parecido, un fragmento lejano se descarta");
        Assert.That(tambienPorPalabras.Select(fragmento => fragmento.Id), Is.EqualTo(new[] { fragmentoId }),
            "si contiene las palabras de la consulta, pasa al reranker aunque esté lejos");
    }

    [Test]
    public async Task ReprocesarUnDocumentoInexistenteResponde404()
    {
        var respuesta = await Cliente.PostAsync($"/api/v1/documentos-tecnicos/{long.MaxValue}/reprocesar", null);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task ReprocesarUnDocumentoQueNoUsaLaIaSeRechazaSinTocarlo()
    {
        var creado = await (await SubirAsync("sin-ia-" + DatosDePrueba.Sufijo() + ".pdf")).LeerJsonAsync();
        var id = creado.GetProperty("id").GetInt64();

        var respuesta = await Cliente.PostAsync($"/api/v1/documentos-tecnicos/{id}/reprocesar", null);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await CodigoAsync(respuesta), Is.EqualTo("DOCUMENTO_SIN_SUGERENCIAS_IA"));
        var documento = await (await Cliente.GetAsync($"/api/v1/documentos-tecnicos/{id}")).LeerJsonAsync();
        Assert.That(documento.GetProperty("cantidadFragmentos").GetInt32(), Is.EqualTo(0));
    }

    [Test]
    public async Task CambiarLaVigenciaDeUnDocumentoInexistenteResponde404()
    {
        var respuesta = await Cliente.PatchAsJsonAsync($"/api/v1/documentos-tecnicos/{long.MaxValue}/vigencia", new { vigente = false });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
