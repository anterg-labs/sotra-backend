using Api.Servicios;

namespace Api.PruebasUnitarias;

public class ServiciosDelManualPruebas
{
    private static SeccionDocumento Seccion(string titulo, string cuerpo, string contexto = "", long id = 1,
        int pagina = 1, string documento = "manual.pdf", bool llegaAlFinal = false) =>
        new(id, pagina, documento, titulo, contexto, cuerpo, llegaAlFinal);

    // DocumentoPdf

    [Test]
    public void Normalizar_QuitaControlGuionInvisibleYColapsaEspacios()
    {
        var texto = "  hola\u0001­   mun­do \n\t fin  ";

        Assert.That(DocumentoPdf.Normalizar(texto), Is.EqualTo("hola mundo fin"));
    }

    [Test]
    public void Normalizar_AplicaNormalizacionDeCompatibilidad()
    {
        Assert.That(DocumentoPdf.Normalizar("ﬁltro №"), Is.EqualTo("filtro No"));
    }

    [Test]
    public void Normalizar_TextoVacioYSoloEspacios()
    {
        Assert.That(DocumentoPdf.Normalizar(""), Is.EqualTo(""));
        Assert.That(DocumentoPdf.Normalizar(" \t\n "), Is.EqualTo(""));
    }

    [Test]
    public void Dividir_TextoVacioNoGeneraFragmentos()
    {
        Assert.That(DocumentoPdf.Dividir(""), Is.Empty);
    }

    [Test]
    public void Dividir_TextoCortoEsUnSoloFragmento()
    {
        Assert.That(DocumentoPdf.Dividir("texto corto"), Is.EqualTo(new[] { "texto corto" }));
    }

    [Test]
    public void Dividir_TextoLargoSeParteRespetandoElMaximoYSolapando()
    {
        var palabras = Enumerable.Range(0, 1200).Select(i => "palabra" + i);
        var texto = string.Join(' ', palabras);

        var fragmentos = DocumentoPdf.Dividir(texto);

        Assert.That(fragmentos.Count, Is.GreaterThan(5));
        Assert.That(fragmentos, Has.All.Property("Length").LessThanOrEqualTo(1400));
        Assert.That(fragmentos, Has.All.Property("Length").GreaterThan(0));
        for (var indice = 1; indice < fragmentos.Count; indice++)
        {
            var colaAnterior = fragmentos[indice - 1].Split(' ').TakeLast(3);
            Assert.That(fragmentos[indice].Split(' ').Intersect(colaAnterior), Is.Not.Empty,
                "fragmentos consecutivos deben repetir texto");
        }

        Assert.That(fragmentos[^1], Does.EndWith("palabra1199"));
    }

    [Test]
    public void Dividir_TextoSinEspaciosSeCortaEnElMaximo()
    {
        var fragmentos = DocumentoPdf.Dividir(new string('a', 3000));

        Assert.That(fragmentos[0].Length, Is.EqualTo(1400));
        Assert.That(fragmentos.Count, Is.GreaterThanOrEqualTo(3));
        Assert.That(fragmentos, Has.All.Property("Length").LessThanOrEqualTo(1400));
    }

    [Test]
    public void Extraer_ArchivoQueNoEsPdfLanzaExcepcion()
    {
        Assert.Catch(() => DocumentoPdf.Extraer(System.Text.Encoding.ASCII.GetBytes("esto no es un pdf")));
    }

    // TextoDelManual

    [Test]
    public void CrearTituloLegible_CapitalizaLaPrimeraLetra()
    {
        Assert.That(TextoDelManual.CrearTituloLegible("CAMBIO DEL ACEITE"), Is.EqualTo("Cambio del aceite"));
        Assert.That(TextoDelManual.CrearTituloLegible(""), Is.EqualTo(""));
        Assert.That(TextoDelManual.CrearTituloLegible("Ñandú"), Is.EqualTo("Ñandú"));
    }

    [Test]
    public void QuitarMarcasInternas_ReemplazaLaMarcaYColapsaEspacios()
    {
        var texto = "uno " + TextoDelManual.MarcaDeSubtitulo + "  dos   tres";

        Assert.That(TextoDelManual.QuitarMarcasInternas(texto), Is.EqualTo("uno dos tres"));
    }

    [Test]
    public void RecortarEnOracion_TextoCortoNoCambia()
    {
        Assert.That(TextoDelManual.RecortarEnOracion("Hola.", 10), Is.EqualTo("Hola."));
        Assert.That(TextoDelManual.RecortarEnOracion("1234567890", 10), Is.EqualTo("1234567890"));
    }

    [Test]
    public void RecortarEnOracion_CortaEnElUltimoPunto()
    {
        var resultado = TextoDelManual.RecortarEnOracion("Primera oración. Segunda oración larga.", 25);

        Assert.That(resultado, Is.EqualTo("Primera oración."));
    }

    [Test]
    public void RecortarEnOracion_SinPuntoCortaEnPalabraYAgregaPuntosSuspensivos()
    {
        var resultado = TextoDelManual.RecortarEnOracion("uno dos tres cuatro cinco", 14);

        Assert.That(resultado, Is.EqualTo("uno dos tres…"));
    }

    [Test]
    public void RecortarEnOracion_SinEspaciosCortaDuro()
    {
        Assert.That(TextoDelManual.RecortarEnOracion("abcdefghijklmnop", 5), Is.EqualTo("abcde…"));
    }

    [Test]
    public void RecortarEnOracion_NuncaSuperaElLargoMasLaElipsis()
    {
        var texto = string.Join(' ', Enumerable.Repeat("palabra", 200));

        Assert.That(TextoDelManual.RecortarEnOracion(texto, 100).Length, Is.LessThanOrEqualTo(101));
    }

    [Test]
    public void CrearResumenExtractivo_QuitaVinetasYMarcas()
    {
        var resumen = TextoDelManual.CrearResumenExtractivo("• Aflojar el tornillo " + TextoDelManual.MarcaDeSubtitulo + " • Retirar la tapa");

        Assert.That(resumen, Is.EqualTo("Aflojar el tornillo Retirar la tapa"));
    }

    [Test]
    public void CrearResumenExtractivo_RecortaA500Caracteres()
    {
        var texto = string.Join(' ', Enumerable.Repeat("palabra", 200));

        Assert.That(TextoDelManual.CrearResumenExtractivo(texto).Length, Is.LessThanOrEqualTo(501));
    }

    [Test]
    public void UnirContinuacion_SinRepeticionConcatenaConEspacio()
    {
        Assert.That(TextoDelManual.UnirContinuacion("abc", "def"), Is.EqualTo("abc def"));
    }

    [Test]
    public void UnirContinuacion_ConSolapamientoNoDuplicaElTexto()
    {
        var cuerpo = "Inicio del procedimiento. Retire la tapa del depósito de aceite y limpie el filtro";
        var final = cuerpo.Substring(cuerpo.Length - 40);
        var conRepeticion = "algo previo " + final + " con un paño seco.";

        var unido = TextoDelManual.UnirContinuacion(cuerpo, conRepeticion);

        Assert.That(unido, Is.EqualTo(cuerpo + " con un paño seco."));
    }

    [Test]
    public void LimpiarRuido_QuitaEncabezadosDePaginaYColapsaEspacios()
    {
        var limpio = TextoDelManual.LimpiarRuido("MANTENIMIENTO GENERAL 6-12 Revise   el nivel.");

        Assert.That(limpio, Does.Not.Contain("6-12"));
        Assert.That(limpio, Does.Contain("Revise el nivel."));
    }

    [Test]
    public void LimpiarRuido_CorrigeVinetasMalExtraidas()
    {
        Assert.That(TextoDelManual.LimpiarRuido("Pasos: l Apague el equipo l Desconecte"), Does.Contain("• Apague el equipo • Desconecte"));
    }

    [Test]
    public void LimpiarRuido_TextoVacio()
    {
        Assert.That(TextoDelManual.LimpiarRuido(""), Is.EqualTo(""));
    }

    [Test]
    public void DividirEnSecciones_SinTitulosDevuelveUnaSeccionSinTitulo()
    {
        var fuente = new FuenteDocumento(5, 3, "esto es solo texto corrido sin ningún título en mayúsculas", "m.pdf");

        var secciones = TextoDelManual.DividirEnSecciones(fuente);

        Assert.That(secciones, Has.Count.EqualTo(1));
        Assert.That(secciones[0].Titulo, Is.EqualTo(""));
        Assert.That(secciones[0].LlegaAlFinal, Is.True);
        Assert.That(secciones[0].Pagina, Is.EqualTo(3));
        Assert.That(secciones[0].FragmentoId, Is.EqualTo(5));
        Assert.That(secciones[0].Documento, Is.EqualTo("m.pdf"));
    }

    [Test]
    public void DividirEnSecciones_DetectaTitulosEnMayusculas()
    {
        var texto = "CAMBIO DE ACEITE Drene el aceite usado y reemplace el filtro. " +
                    "LIMPIEZA DEL FILTRO Retire el filtro y lávelo con agua limpia.";

        var secciones = TextoDelManual.DividirEnSecciones(new FuenteDocumento(1, 1, texto, "m.pdf"));

        Assert.That(secciones.Select(seccion => seccion.Titulo), Is.EqualTo(new[] { "CAMBIO DE ACEITE", "LIMPIEZA DEL FILTRO" }));
        Assert.That(secciones[0].Cuerpo, Does.StartWith("Drene el aceite"));
        Assert.That(secciones[0].LlegaAlFinal, Is.False);
        Assert.That(secciones[1].LlegaAlFinal, Is.True);
    }

    [Test]
    public void DividirEnSecciones_TextoAntesDelPrimerTituloQuedaSinTitulo()
    {
        var texto = "Texto introductorio previo. CAMBIO DE ACEITE Drene el aceite usado y reemplace el filtro.";

        var secciones = TextoDelManual.DividirEnSecciones(new FuenteDocumento(1, 1, texto, "m.pdf"));

        Assert.That(secciones[0].Titulo, Is.EqualTo(""));
        Assert.That(secciones[0].Cuerpo, Is.EqualTo("Texto introductorio previo."));
        Assert.That(secciones[1].Titulo, Is.EqualTo("CAMBIO DE ACEITE"));
    }

    [Test]
    public void DividirEnSecciones_TextoVacioNoGeneraSecciones()
    {
        Assert.That(TextoDelManual.DividirEnSecciones(new FuenteDocumento(1, 1, "   ", "m.pdf")), Is.Empty);
    }

    [Test]
    public void DividirEnSecciones_ReferenciaCruzadaNoEsTitulo()
    {
        var texto = "Para más detalle vea MANTENIMIENTO PERIODICO (6-1) en el capítulo anterior del manual.";

        var secciones = TextoDelManual.DividirEnSecciones(new FuenteDocumento(1, 1, texto, "m.pdf"));

        Assert.That(secciones, Has.Count.EqualTo(1));
        Assert.That(secciones[0].Titulo, Is.EqualTo(""));
    }

    // AdvertenciasDelManual

    [Test]
    public void Advertencias_ExtraeElTextoDespuesDeLaPalabraClave()
    {
        var advertencias = AdvertenciasDelManual.Extraer("Paso uno. ADVERTENCIA Desconecte la batería antes de trabajar. Paso dos.");

        Assert.That(advertencias, Has.Count.EqualTo(1));
        Assert.That(advertencias[0], Does.StartWith("Desconecte la batería"));
    }

    [Test]
    public void Advertencias_SinPalabrasClaveDevuelveListaVacia()
    {
        Assert.That(AdvertenciasDelManual.Extraer("Revise el nivel de aceite cada día."), Is.Empty);
        Assert.That(AdvertenciasDelManual.Extraer(""), Is.Empty);
    }

    [Test]
    public void Advertencias_NoSuperaElMaximoDeCuatro()
    {
        var texto = string.Join(" ", Enumerable.Range(1, 8).Select(i => $"PELIGRO Advertencia numero {i} importante para la seguridad."));

        Assert.That(AdvertenciasDelManual.Extraer(texto).Count, Is.LessThanOrEqualTo(4));
    }

    [Test]
    public void Advertencias_NoRepiteLasIguales()
    {
        var texto = "AVISO Use guantes de protección. AVISO Use guantes de protección.";

        Assert.That(AdvertenciasDelManual.Extraer(texto), Has.Count.EqualTo(1));
    }

    [Test]
    public void Advertencias_RecortaLasMuyLargas()
    {
        var texto = "ADVERTENCIA " + string.Join(' ', Enumerable.Repeat("no toque el motor caliente.", 40));

        Assert.That(AdvertenciasDelManual.Extraer(texto).All(item => item.Length <= 351), Is.True);
    }

    [Test]
    public void Advertencias_IgnoraReferenciasCruzadas()
    {
        Assert.That(AdvertenciasDelManual.Extraer("IMPORTANTE Vea el procedimiento (6-12) para más datos."), Is.Empty);
    }

    [Test]
    public void Advertencias_Quitar_EliminaElTextoYLaPalabraClave()
    {
        var cuerpo = "Retire la tapa. ADVERTENCIA Desconecte la batería. Limpie el filtro.";

        var sinAdvertencias = AdvertenciasDelManual.Quitar(cuerpo);

        Assert.That(sinAdvertencias, Does.Not.Contain("ADVERTENCIA"));
        Assert.That(sinAdvertencias, Does.Not.Contain("Desconecte la batería"));
        Assert.That(sinAdvertencias, Does.Contain("Retire la tapa."));
    }

    [Test]
    public void Advertencias_Quitar_SinAdvertenciasSoloNormalizaEspacios()
    {
        Assert.That(AdvertenciasDelManual.Quitar("  uno   dos "), Is.EqualTo("uno dos"));
    }

    // SeccionesCandidatas

    [Test]
    public void Candidatas_Armar_ListaVaciaDevuelveVacio()
    {
        Assert.That(SeccionesCandidatas.Armar([]), Is.Empty);
    }

    [Test]
    public void Candidatas_Armar_DescartaSeccionesMuyCortas()
    {
        var corto = new FuenteDocumento(1, 1, "texto corto", "m.pdf");
        var largo = new FuenteDocumento(2, 1, new string('a', 20) + " " + string.Join(' ', Enumerable.Repeat("palabra", 20)), "m.pdf");

        var secciones = SeccionesCandidatas.Armar([corto, largo]);

        Assert.That(secciones, Has.Count.EqualTo(1));
        Assert.That(secciones[0].FragmentoId, Is.EqualTo(2));
        Assert.That(secciones[0].Cuerpo.Length, Is.GreaterThanOrEqualTo(SeccionesCandidatas.LargoMinimoDeSeccion));
    }

    [Test]
    public void Candidatas_Armar_OrdenaPorIdYNoImportaElOrdenDeEntrada()
    {
        var textoA = "CAMBIO DE ACEITE " + string.Join(' ', Enumerable.Repeat("drene el aceite usado", 6));
        var textoB = "LIMPIEZA DEL FILTRO " + string.Join(' ', Enumerable.Repeat("retire el filtro limpio", 6));
        var a = new FuenteDocumento(1, 1, textoA, "m.pdf");
        var b = new FuenteDocumento(2, 1, textoB, "m.pdf");

        var directo = SeccionesCandidatas.Armar([a, b]).Select(seccion => seccion.Titulo);
        var invertido = SeccionesCandidatas.Armar([b, a]).Select(seccion => seccion.Titulo);

        Assert.That(invertido, Is.EqualTo(directo));
    }

    [Test]
    public void Candidatas_Armar_UneUnaSeccionPartidaEntreFragmentosConsecutivos()
    {
        var primera = "CAMBIO DE ACEITE " + string.Join(' ', Enumerable.Repeat("drene el aceite usado de la maquina", 3));
        var segunda = "y reemplace luego el filtro y la tapa del deposito con cuidado extremo.";
        var fuentes = new List<FuenteDocumento>
        {
            new(1, 1, primera, "m.pdf"),
            new(2, 2, segunda, "m.pdf"),
        };

        var secciones = SeccionesCandidatas.Armar(fuentes);

        Assert.That(secciones, Has.Count.EqualTo(1));
        Assert.That(secciones[0].Cuerpo, Does.Contain("reemplace luego el filtro"));
        Assert.That(secciones[0].Pagina, Is.EqualTo(1));
    }

    [Test]
    public void Candidatas_Armar_NoUneFragmentosDeDocumentosDistintos()
    {
        var primera = "CAMBIO DE ACEITE " + string.Join(' ', Enumerable.Repeat("drene el aceite usado de la maquina", 3));
        var segunda = "y reemplace luego el filtro y la tapa del deposito con cuidado extremo en el otro manual.";

        var secciones = SeccionesCandidatas.Armar(
        [
            new FuenteDocumento(1, 1, primera, "a.pdf"),
            new FuenteDocumento(2, 1, segunda, "b.pdf"),
        ]);

        Assert.That(secciones, Has.Count.EqualTo(2));
    }

    [Test]
    public void Candidatas_Armar_ConservaLaMasLargaDeLasRepetidas()
    {
        var corta = "CAMBIO DE ACEITE " + string.Join(' ', Enumerable.Repeat("drene el aceite usado", 5));
        var larga = "CAMBIO DE ACEITE " + string.Join(' ', Enumerable.Repeat("drene el aceite usado", 12));

        var secciones = SeccionesCandidatas.Armar(
        [
            new FuenteDocumento(1, 1, corta, "m.pdf"),
            new FuenteDocumento(5, 9, larga, "m.pdf"),
        ]);

        Assert.That(secciones, Has.Count.EqualTo(1));
        Assert.That(secciones[0].FragmentoId, Is.EqualTo(5));
    }

    [Test]
    public void Candidatas_BuscarOpciones_SinTituloDevuelveSoloLaPrincipal()
    {
        var principal = Seccion("", "cuerpo");

        var opciones = SeccionesCandidatas.BuscarOpcionesEquivalentes("cambio de aceite", [principal, Seccion("CAMBIO DE ACEITE", "x")]);

        Assert.That(opciones, Is.EqualTo(new[] { principal }));
    }

    [Test]
    public void Candidatas_BuscarOpciones_SinRaicesCompartidasDevuelveSoloLaPrincipal()
    {
        var principal = Seccion("LIMPIEZA DEL FILTRO", "x");

        var opciones = SeccionesCandidatas.BuscarOpcionesEquivalentes("cambio de aceite", [principal, Seccion("CAMBIO DE ACEITE", "y")]);

        Assert.That(opciones, Has.Count.EqualTo(1));
    }

    [Test]
    public void Candidatas_BuscarOpciones_AgregaLasQueComparteLasRaices()
    {
        var principal = Seccion("CAMBIO DE ACEITE DEL MOTOR", "a", id: 1);
        var otra = Seccion("CAMBIO DE ACEITE HIDRAULICO", "b", id: 2);
        var ajena = Seccion("LIMPIEZA DEL FILTRO", "c", id: 3);
        var sinTitulo = Seccion("", "d", id: 4);

        var opciones = SeccionesCandidatas.BuscarOpcionesEquivalentes("como cambiar el aceite",
            [principal, ajena, sinTitulo, otra]);

        Assert.That(opciones, Is.EqualTo(new[] { principal, otra }));
    }

    [Test]
    public void Candidatas_BuscarOpciones_NoSuperaCincoOpciones()
    {
        var secciones = Enumerable.Range(1, 10)
            .Select(i => Seccion($"CAMBIO DE ACEITE TIPO {i}", "x", id: i))
            .ToList();

        Assert.That(SeccionesCandidatas.BuscarOpcionesEquivalentes("cambio aceite", secciones), Has.Count.EqualTo(5));
    }

    [Test]
    public void Candidatas_BuscarOpciones_IgnoraTildesYMayusculas()
    {
        var principal = Seccion("LUBRICACIÓN GENERAL", "a", id: 1);
        var otra = Seccion("LUBRICACION DE RODAMIENTOS", "b", id: 2);

        var opciones = SeccionesCandidatas.BuscarOpcionesEquivalentes("lubricación", [principal, otra]);

        Assert.That(opciones, Has.Count.EqualTo(2));
    }

    // RedaccionDeSugerencia

    [Test]
    public void Redaccion_EsResumenConfiable_RechazaMuyCortoOMuyLargo()
    {
        var seccion = Seccion("T", "cuerpo");

        Assert.That(RedaccionDeSugerencia.EsResumenConfiable("corto", "q", seccion), Is.False);
        Assert.That(RedaccionDeSugerencia.EsResumenConfiable(new string('a', 1201), "q", seccion), Is.False);
        Assert.That(RedaccionDeSugerencia.EsResumenConfiable(new string('a', 20), "q", seccion), Is.True);
        Assert.That(RedaccionDeSugerencia.EsResumenConfiable(new string('a', 1200), "q", seccion), Is.True);
    }

    [Test]
    public void Redaccion_EsResumenConfiable_RechazaNumerosInventados()
    {
        var seccion = Seccion("CAMBIO", "Use 5 litros de aceite cada 250 horas.");

        Assert.That(RedaccionDeSugerencia.EsResumenConfiable("Se deben usar 5 litros de aceite cada 250 horas.", "q", seccion), Is.True);
        Assert.That(RedaccionDeSugerencia.EsResumenConfiable("Se deben usar 7 litros de aceite cada 250 horas.", "q", seccion), Is.False);
    }

    [Test]
    public void Redaccion_EsResumenConfiable_EquiparaComaYPuntoDecimal()
    {
        var seccion = Seccion("T", "Ajuste a 2,5 bar de presión en el circuito.");

        Assert.That(RedaccionDeSugerencia.EsResumenConfiable("Ajuste la presión a 2.5 bar en el circuito.", "q", seccion), Is.True);
        Assert.That(RedaccionDeSugerencia.EsResumenConfiable("Ajuste la presión a 2,5 bar en el circuito.", "q", seccion), Is.True);
    }

    [Test]
    public void Redaccion_EsResumenConfiable_AceptaNumerosQueVienenDeLaConsulta()
    {
        var seccion = Seccion("T", "Texto sin cifras en el manual de la maquina.");

        Assert.That(RedaccionDeSugerencia.EsResumenConfiable("Para la máquina 300 revise todo el circuito.", "modelo 300", seccion), Is.True);
    }

    [Test]
    public void Redaccion_CrearMensajeParaQwen_IncluyeConsultaTituloContextoYTexto()
    {
        var seccion = Seccion("CAMBIO DE ACEITE", "cuerpo", contexto: "Contexto previo.");

        var mensaje = RedaccionDeSugerencia.CrearMensajeParaQwen("¿cómo cambio el aceite?", seccion, "Drene " + TextoDelManual.MarcaDeSubtitulo + " el aceite.");

        Assert.Multiple(() =>
        {
            Assert.That(mensaje, Does.Contain("Consulta del técnico: ¿cómo cambio el aceite?"));
            Assert.That(mensaje, Does.Contain("Sección del manual: Cambio de aceite"));
            Assert.That(mensaje, Does.Contain("Contexto previo de la sección: Contexto previo."));
            Assert.That(mensaje, Does.Contain("Drene el aceite."));
            Assert.That(mensaje, Does.Not.Contain(TextoDelManual.MarcaDeSubtitulo));
            Assert.That(mensaje, Does.EndWith("Escribí el resumen."));
        });
    }

    [Test]
    public void Redaccion_CrearMensajeParaQwen_SinContextoNoLoMenciona()
    {
        var mensaje = RedaccionDeSugerencia.CrearMensajeParaQwen("q", Seccion("T", "c"), "texto");

        Assert.That(mensaje, Does.Not.Contain("Contexto previo"));
    }

    [Test]
    public void Redaccion_CrearMensajeParaQwen_RecortaElTextoA3000()
    {
        var mensaje = RedaccionDeSugerencia.CrearMensajeParaQwen("q", Seccion("T", "c"), new string('x', 5000));

        Assert.That(mensaje, Does.Contain(new string('x', 3000)));
        Assert.That(mensaje, Does.Not.Contain(new string('x', 3001)));
    }

    [Test]
    public void Redaccion_ArmarRespuesta_SoloResumenYFuente()
    {
        var elegida = Seccion("CAMBIO DE ACEITE", "c", pagina: 12, documento: "manual.pdf");

        var respuesta = RedaccionDeSugerencia.ArmarRespuesta("Se drena el aceite.", [], elegida, [elegida]);

        Assert.That(respuesta, Is.EqualTo("Se drena el aceite.\n\nFuente:\nmanual.pdf, página 12, sección \"Cambio de aceite\"."));
    }

    [Test]
    public void Redaccion_ArmarRespuesta_SinTituloOmiteLaSeccionEnLaFuente()
    {
        var elegida = Seccion("", "c", pagina: 2, documento: "m.pdf");

        var respuesta = RedaccionDeSugerencia.ArmarRespuesta("Resumen.", [], elegida, [elegida]);

        Assert.That(respuesta, Does.EndWith("Fuente:\nm.pdf, página 2."));
    }

    [Test]
    public void Redaccion_ArmarRespuesta_IncluyeContextoYAdvertencias()
    {
        var elegida = Seccion("T", "c", contexto: "Contexto.");

        var respuesta = RedaccionDeSugerencia.ArmarRespuesta("Resumen.", ["Use guantes.", "No fume."], elegida, [elegida]);

        Assert.That(respuesta, Does.StartWith("Contexto.\nResumen.\n"));
        Assert.That(respuesta, Does.Contain("Advertencias:\n- Use guantes.\n- No fume.\n"));
    }

    [Test]
    public void Redaccion_ArmarRespuesta_ConVariasOpcionesListaLasOtras()
    {
        var elegida = Seccion("CAMBIO DE ACEITE", "c", pagina: 3);
        var otra = Seccion("CAMBIO DE FILTRO", "c", pagina: 9);

        var respuesta = RedaccionDeSugerencia.ArmarRespuesta("Resumen.", [], elegida, [elegida, otra]);

        Assert.Multiple(() =>
        {
            Assert.That(respuesta, Does.Contain("Aclaración:"));
            Assert.That(respuesta, Does.Contain("- Cambio de filtro (página 9)"));
            Assert.That(respuesta, Does.Not.Contain("- Cambio de aceite (página"));
            Assert.That(respuesta, Does.Contain("Indique cuál necesita"));
        });
    }

    [Test]
    public void Redaccion_InstruccionesParaQwen_ProhibenInventarDatos()
    {
        Assert.That(RedaccionDeSugerencia.InstruccionesParaQwen, Does.Contain("No agregues pasos"));
        Assert.That(RedaccionDeSugerencia.InstruccionesParaQwen, Does.Contain("Copiá exactamente los números"));
    }
}
