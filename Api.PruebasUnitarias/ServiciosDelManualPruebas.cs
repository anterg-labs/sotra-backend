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
    public void MarcarTitulosNumerados_MarcaLosTitulosConLineaPropia()
    {
        var marca = TextoDelManual.MarcaDeTituloNumerado;
        var texto = "4. Sistema hidraulico\r\n4.2 Cambio del filtro de aceite hidraulico\n" +
                    "1. Estacione la maquina en terreno plano.\n6.1 Bateria";

        var marcado = DocumentoPdf.MarcarTitulosNumerados(texto);

        Assert.That(marcado.Split('\n'), Is.EqualTo(new[]
        {
            marca + "4. Sistema hidraulico" + marca,
            marca + "4.2 Cambio del filtro de aceite hidraulico" + marca,
            "1. Estacione la maquina en terreno plano.",
            marca + "6.1 Bateria" + marca
        }));
    }

    [TestCase("1. Retire el filtro y limpie la junta.", TestName = "MarcarTitulosNumerados_PasoQueTerminaEnPunto")]
    [TestCase("2 Filtro de aire", TestName = "MarcarTitulosNumerados_NumeroSinPunto")]
    [TestCase("3. 25 Nm de par de apriete", TestName = "MarcarTitulosNumerados_TextoQueNoEmpiezaConMayuscula")]
    [TestCase("Nota: revise el nivel", TestName = "MarcarTitulosNumerados_LineaSinNumero")]
    [TestCase("ADVERTENCIA: apague el motor", TestName = "MarcarTitulosNumerados_Advertencia")]
    [TestCase("5. Encienda el motor dos minutos y accione el brazo varias veces hasta que no haya perdidas",
        TestName = "MarcarTitulosNumerados_LineaConDemasiadasPalabras")]
    public void MarcarTitulosNumerados_NoMarcaLoQueNoEsTitulo(string linea)
    {
        Assert.That(DocumentoPdf.MarcarTitulosNumerados(linea), Is.EqualTo(linea));
    }

    [TestCase("2. Follow the Lock-out Tag-out procedure in the Safety\nsection of this manual.",
        TestName = "MarcarTitulosNumerados_PasoQueSigueEnLaLineaSiguiente")]
    [TestCase("1. Open the right rear access panel of your machine to\nLocate the primary fuel filter (a).",
        TestName = "MarcarTitulosNumerados_PasoQueTerminaEnUnConector")]
    [TestCase("3. Afloje el tapón de drenaje del\ncárter.", TestName = "MarcarTitulosNumerados_PasoEnEspanolCortado")]
    [TestCase("3. Open the right door and turn the filter\n(1) leftward with filter spanner.",
        TestName = "MarcarTitulosNumerados_PasoQueSigueDespuesDeUnaReferencia")]
    [TestCase("PELIGRO DE APLASTAMIENTO\nNo se ubique debajo del brazo.",
        TestName = "MarcarTitulosNumerados_MayusculasConPalabraDeAviso")]
    [TestCase("Change:\n1. Open the panel.", TestName = "MarcarTitulosNumerados_RotuloConDosPuntosAntesDeUnPaso")]
    [TestCase("TS1671 –UN–01MAY03\nRevise el nivel.", TestName = "MarcarTitulosNumerados_CodigoDeFiguraEnMayusculas")]
    [TestCase("RESPONSABILIDADES DEL PROPIETARIO EN EL PERIODO DE\nGARANTIA", TestName = "MarcarTitulosNumerados_MayusculasCortadas")]
    [TestCase("3. The authorized person who performed the Lockout/\nTagout must remove the tag.",
        TestName = "MarcarTitulosNumerados_PasoQueTerminaEnBarra")]
    [TestCase("Retire la tapa\ncon cuidado.", TestName = "MarcarTitulosNumerados_LineaSinPasoSiguiente")]
    public void MarcarTitulosNumerados_NoMarcaLasLineasQueSonPartesDeUnaOracion(string texto)
    {
        Assert.That(DocumentoPdf.MarcarTitulosNumerados(texto), Is.EqualTo(texto));
    }

    [Test]
    public void MarcarTitulosNumerados_MarcaElTituloEnMayusculasConSuNumeroYElQueEncabezaUnProcedimiento()
    {
        var marca = TextoDelManual.MarcaDeTituloNumerado;
        var texto = "500 HOURS OF SERVICE\n1. Park the machine on a level flat surface (a).\n" +
                    "Primary Fuel Filter\n1. Open the right rear access panel.";

        var marcado = DocumentoPdf.MarcarTitulosNumerados(texto);

        Assert.That(marcado.Split('\n'), Is.EqualTo(new[]
        {
            marca + "500 HOURS OF SERVICE" + marca,
            "1. Park the machine on a level flat surface (a).",
            marca + "Primary Fuel Filter" + marca,
            "1. Open the right rear access panel."
        }));
    }

    [Test]
    public void DividirEnSecciones_UsaLosTitulosSinNumeroMarcadosYNoEmparejaUnaMarcaDeCierreSuelta()
    {
        var marca = TextoDelManual.MarcaDeTituloNumerado;
        var texto = "Filter" + marca + " 1. Open the panel. " + marca + "500 HOURS OF SERVICE" + marca +
                    " 1. Park the machine. " + marca + "Primary Fuel Filter" + marca + " 1. Drain the fuel.";

        var secciones = TextoDelManual.DividirEnSecciones(new FuenteDocumento(1, 1, texto, "manual.pdf"));

        Assert.That(secciones.Select(seccion => seccion.Titulo),
            Is.EqualTo(new[] { "", "500 HOURS OF SERVICE", "Primary Fuel Filter" }));
        Assert.That(secciones[0].Cuerpo, Is.EqualTo("Filter 1. Open the panel."));
    }

    [Test]
    public void EsIndice_ReconoceLasLineasDePuntosDelIndice()
    {
        var indice = "6.5.1 Testing ..........6-13 6.5.2 Adjusting ..........6-15 6.6 Engine Oil Pressure ........6-17";

        Assert.That(TextoDelManual.EsIndice(indice), Is.True);
        Assert.That(TextoDelManual.EsIndice("Afloje el tapón... y espere. Drene el aceite."), Is.False);
    }

    // Valores medidos con consultas en español contra los manuales SANY en inglés.
    [TestCase(1.59f, 0.332, true, TestName = "EsSeccionConfiable_RerankerPositivo")]
    [TestCase(0.27f, null, true, TestName = "EsSeccionConfiable_RerankerPositivoSinDistancia")]
    [TestCase(-1.09f, 0.373, true, TestName = "EsSeccionConfiable_EntreIdiomasCercana")]
    [TestCase(-1.22f, 0.348, true, TestName = "EsSeccionConfiable_EntreIdiomasCercanaOtroManual")]
    [TestCase(-1.09f, 0.45, false, TestName = "EsSeccionConfiable_EntreIdiomasLejana")]
    [TestCase(-1.09f, null, false, TestName = "EsSeccionConfiable_EntreIdiomasSoloPorPalabras")]
    [TestCase(-2.59f, 0.435, false, TestName = "EsSeccionConfiable_ConsultaSinRespuesta")]
    [TestCase(-2.89f, 0.39, false, TestName = "EsSeccionConfiable_RerankerMuyBajoAunqueCercana")]
    public void ServicioSugerencias_EsSeccionConfiable(float puntaje, double? distancia, bool esperado)
    {
        Assert.That(ServicioSugerencias.EsSeccionConfiable(puntaje, distancia), Is.EqualTo(esperado));
    }

    [Test]
    public void Extraer_ArchivoQueNoEsPdfLanzaExcepcion()
    {
        Assert.Catch(() => DocumentoPdf.Extraer(System.Text.Encoding.ASCII.GetBytes("esto no es un pdf")));
    }

    // Páginas como las extrae PdfPig del manual SY335 de SANY America: las pares y las impares tienen encabezados
    // distintos, las impares repiten un índice lateral de una palabra por línea y todas terminan con la dirección web
    // del sitio de descarga. Las pares de servicio terminan con la figura de bloqueo ("DO NOT OPERATE").
    private static readonly string[] IndiceLateral =
    {
        "TABLE", "OF", "CONTENTS", "INTRODUCTION", "SAFETY", "SYSTEM", "FUNCTIONS", "OPERATION", "MAINTENANCE",
        "TROBLESHOOTING", "SPECIFICATIONS", "OPTIONAL", "EQUIPMENT"
    };

    private static readonly string[] FiguraDeBloqueo = { "Fig. 5-108", "a", "DO NOTOPERATE", "DANGER", "Fig. 5-109" };

    private static string PaginaDelSy335(int numero, IEnumerable<string> cuerpo, bool terminaConLaFiguraDeBloqueo = false)
    {
        var lineas = new List<string>();
        if (numero % 2 == 0)
        {
            lineas.Add("500 Hours of Service SY335 Excavator");
            lineas.Add($"5-{numero - 184}                                     Safety, Operation & Maintenance Manual — November 28, 2013                    SANY AMERICA");
        }
        else
        {
            lineas.Add("SY335 Excavator 500 Hours of Service");
            lineas.Add($"SANY AMERICA Safety, Operation & Maintenance Manual — November 28, 2013 5-{numero - 184}");
            lineas.AddRange(IndiceLateral);
        }

        lineas.AddRange(cuerpo);
        if (terminaConLaFiguraDeBloqueo)
        {
            lineas.AddRange(FiguraDeBloqueo);
        }

        lineas.Add("https://truckmanualshub.com/");
        return string.Join("\n", lineas);
    }

    private static readonly string[] CuerpoDeLaPagina258 =
    {
        "500 HOURS OF SERVICE",
        "1. Park the machine on a level flat surface (a).",
        "2. Follow the Lock-out Tag-out procedure in the Safety",
        "section of this manual. Always allow the systems time to",
        "cool down before proceeding with any service.",
        "3. With the machine secured, proceed with the following:",
        "Change:",
        "• Primary Fuel Filter (see page 5-75)",
        "• Coolant Filter (see page 5-77)",
        "Checks:",
        "• Air Conditioning Fresh Air and Recirculation Air Filters (see page 5-82)",
        "WARNINGNOTICE",
        "Failure to perform the following procedures when and how directed will result in shortened",
        "service life of the machine or a system failure during operation."
    };

    private static readonly string[] CuerpoDeLaPagina259 =
    {
        "• Final Drive (see page 5-87)",
        "• Swing Gear Pinion Grease Level (see page 5-88)",
        "Lubricate:",
        "• Work Equipment, (see page 5-56)"
    };

    private static List<string> PaginasDelCapituloDe500Horas() => new()
    {
        PaginaDelSy335(256, new[] { "250 HOURS OF SERVICE", "Grease the swing bearing." }, terminaConLaFiguraDeBloqueo: true),
        PaginaDelSy335(257, new[] { "• Check the battery electrolyte level." }),
        PaginaDelSy335(258, CuerpoDeLaPagina258, terminaConLaFiguraDeBloqueo: true),
        PaginaDelSy335(259, CuerpoDeLaPagina259),
        PaginaDelSy335(260, new[] { "1000 HOURS OF SERVICE", "Change the hydraulic oil." }, terminaConLaFiguraDeBloqueo: true),
        PaginaDelSy335(261, new[] { "• Replace the return filter." })
    };

    [Test]
    public void QuitarEncabezadosYPies_QuitaEncabezadoIndiceLateralFiguraYPieRepetidos()
    {
        var limpias = DocumentoPdf.QuitarEncabezadosYPies(PaginasDelCapituloDe500Horas());

        Assert.That(limpias[2].Split('\n'), Is.EqualTo(CuerpoDeLaPagina258),
            "en la 258 queda solo el contenido, con su título propio, aunque los de otras páginas se parezcan");
        Assert.That(limpias[3].Split('\n'), Is.EqualTo(CuerpoDeLaPagina259),
            "en la 259 se quitan el encabezado y el índice lateral, así empieza con la continuación de la lista");
    }

    [Test]
    public void QuitarEncabezadosYPies_ConservaLaProsaQueSeRepiteAlPie()
    {
        var advertencia = new[] { "Keep clear of the boom. Contact can result in serious injury or", "death." };
        var paginas = new List<string>();
        for (var numero = 256; numero <= 259; numero++)
        {
            paginas.Add(PaginaDelSy335(numero, new[] { "Contenido propio de la página " + numero }.Concat(advertencia)));
        }

        var limpias = DocumentoPdf.QuitarEncabezadosYPies(paginas);

        Assert.That(limpias, Has.All.EndsWith("serious injury or\ndeath."),
            "una advertencia que se repite al pie es contenido, no un pie de página");
    }

    [Test]
    public void QuitarEncabezadosYPies_ConservaArribaLasPalabrasDeAviso()
    {
        var paginas = new List<string>();
        for (var numero = 256; numero <= 261; numero += 2)
        {
            paginas.Add(PaginaDelSy335(numero, new[] { "Note:", "Check the level with the engine stopped " + numero }));
        }

        var limpias = DocumentoPdf.QuitarEncabezadosYPies(paginas);

        Assert.That(limpias, Has.All.StartsWith("Note:\n"));
    }

    [Test]
    public void QuitarEncabezadosYPies_QuitaLasEtiquetasDeFiguraAunqueNoSeRepitan()
    {
        var limpias = DocumentoPdf.QuitarEncabezadosYPies(new List<string>
        {
            "Retire el filtro.\nFigura 12\nFig. 3-4a\nLimpie la junta."
        });

        Assert.That(limpias[0], Is.EqualTo("Retire el filtro.\nLimpie la junta."));
    }

    [Test]
    public void QuitarEncabezadosYPies_ConMenosDeTresPaginasNoQuitaNada()
    {
        var paginas = new List<string> { "Manual X\nPrimera página\n1", "Manual X\nSegunda página\n2" };

        Assert.That(DocumentoPdf.QuitarEncabezadosYPies(paginas), Is.EqualTo(paginas));
    }

    [Test]
    public void QuitarEncabezadosYPies_LaSeccionDeUnaPaginaSeCompletaConLaSiguiente()
    {
        var limpias = DocumentoPdf.QuitarEncabezadosYPies(PaginasDelCapituloDe500Horas());
        var fuente258 = new FuenteDocumento(258, 258, DocumentoPdf.Normalizar(DocumentoPdf.MarcarTitulosNumerados(limpias[2])), "sy335.pdf");
        var fuente259 = new FuenteDocumento(259, 259, DocumentoPdf.Normalizar(DocumentoPdf.MarcarTitulosNumerados(limpias[3])), "sy335.pdf");

        var ultimaDeLa258 = TextoDelManual.DividirEnSecciones(fuente258)[^1];
        var completa = SeccionesCandidatas.CompletarConFragmentoSiguiente(ultimaDeLa258,
            TextoDelManual.DividirEnSecciones(fuente259));

        Assert.That(ultimaDeLa258.LlegaAlFinal, Is.True, "sin la figura de bloqueo, la sección llega al final de la página");
        Assert.That(completa.Cuerpo, Does.Contain("Swing Gear Pinion Grease Level"));
        Assert.That(completa.Cuerpo, Does.Contain("Work Equipment"));
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
    public void CrearTituloLegible_ConNumeroCapitalizaLaPrimeraLetra()
    {
        Assert.That(TextoDelManual.CrearTituloLegible("4.2 CAMBIO DEL FILTRO"), Is.EqualTo("4.2 Cambio del filtro"));
        Assert.That(TextoDelManual.CrearTituloLegible("6.1 Bateria"), Is.EqualTo("6.1 Bateria"));
        Assert.That(TextoDelManual.CrearTituloLegible("12"), Is.EqualTo("12"));
    }

    [Test]
    public void QuitarMarcasInternas_ReemplazaLaMarcaYColapsaEspacios()
    {
        var texto = "uno " + TextoDelManual.MarcaDeSubtitulo + "  dos   tres";

        Assert.That(TextoDelManual.QuitarMarcasInternas(texto), Is.EqualTo("uno dos tres"));
    }

    [Test]
    public void QuitarMarcasInternas_QuitaLaMarcaDeTituloNumerado()
    {
        var texto = "uno " + TextoDelManual.MarcaDeTituloNumerado + "4.3 dos" + TextoDelManual.MarcaDeTituloNumerado + " tres";

        Assert.That(TextoDelManual.QuitarMarcasInternas(texto), Is.EqualTo("uno 4.3 dos tres"));
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

    private static string TextoConTitulosNumerados(string lineas) =>
        DocumentoPdf.Normalizar(DocumentoPdf.MarcarTitulosNumerados(lineas));

    [Test]
    public void DividirEnSecciones_DetectaTitulosNumerados()
    {
        var texto = TextoConTitulosNumerados("Manual de mantenimiento - Retroexcavadora RX-200\n" +
            "4. Sistema hidraulico\n" +
            "4.2 Cambio del filtro de aceite hidraulico\n" +
            "ADVERTENCIA: apague el motor antes de abrir el filtro.\n" +
            "1. Rellene con aceite ISO VG 46.\n" +
            "4.3 Cambio de aceite del motor\n" +
            "El aceite del motor se cambia cada 250 horas. Utilice aceite 15W-40.");

        var secciones = TextoDelManual.DividirEnSecciones(new FuenteDocumento(1, 1, texto, "m.pdf"));

        Assert.That(secciones.Select(seccion => seccion.Titulo), Is.EqualTo(new[]
        {
            "", "4. Sistema hidraulico", "4.2 Cambio del filtro de aceite hidraulico", "4.3 Cambio de aceite del motor"
        }));
        Assert.That(secciones[0].Cuerpo, Is.EqualTo("Manual de mantenimiento - Retroexcavadora RX-200"));
        Assert.That(secciones[2].Contexto, Does.StartWith("4. Sistema hidraulico."));
        Assert.That(secciones[2].Cuerpo, Does.StartWith("ADVERTENCIA").And.Contain("ISO VG 46").And.Not.Contain("15W-40"));
        Assert.That(secciones[3].Cuerpo, Does.StartWith("El aceite del motor").And.Not.Contain("ISO VG 46"));
        Assert.That(secciones[3].LlegaAlFinal, Is.True);
        Assert.That(secciones.Select(seccion => seccion.Cuerpo),
            Has.None.Contain(TextoDelManual.MarcaDeTituloNumerado));
    }

    [Test]
    public void DividirEnSecciones_TituloNumeradoEnMayusculasNoSeDuplica()
    {
        var texto = TextoConTitulosNumerados("4.2 CAMBIO DE ACEITE\nDrene el aceite usado y reemplace el filtro.");

        var secciones = TextoDelManual.DividirEnSecciones(new FuenteDocumento(1, 1, texto, "m.pdf"));

        Assert.That(secciones.Select(seccion => seccion.Titulo), Is.EqualTo(new[] { "4.2 CAMBIO DE ACEITE" }));
        Assert.That(secciones[0].Cuerpo, Is.EqualTo("Drene el aceite usado y reemplace el filtro."));
    }

    [Test]
    public void DividirEnSecciones_TituloNumeradoPartidoEntreFragmentosNoSeEmpareja()
    {
        var marca = TextoDelManual.MarcaDeTituloNumerado;
        var finDelFragmento = "Revise que no haya perdidas. " + marca + "4.3 Cambio de ace";
        var inicioDelSiguiente = "ite del motor" + marca + " El aceite se cambia cada 250 horas. " +
                                 marca + "5. Lubricacion" + marca + " Engrase los puntos del brazo.";

        var primero = TextoDelManual.DividirEnSecciones(new FuenteDocumento(1, 1, finDelFragmento, "m.pdf"));
        var segundo = TextoDelManual.DividirEnSecciones(new FuenteDocumento(2, 1, inicioDelSiguiente, "m.pdf"));

        Assert.That(primero, Has.Count.EqualTo(1));
        Assert.That(primero[0].Titulo, Is.EqualTo(""));
        Assert.That(primero[0].Cuerpo, Is.EqualTo("Revise que no haya perdidas. 4.3 Cambio de ace"));
        Assert.That(segundo.Select(seccion => seccion.Titulo), Is.EqualTo(new[] { "", "5. Lubricacion" }));
        Assert.That(segundo[0].Cuerpo, Is.EqualTo("ite del motor El aceite se cambia cada 250 horas."));
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
    public void Candidatas_Armar_TituloAlFinalRepetidoPorElSolapamientoNoSeUneConTextoAnterior()
    {
        var engrase = "ENGRASE DEL BRAZO Engrase los 4 puntos del brazo cada 50 horas con grasa NLGI 2 y limpie las boquillas.";
        var primera = engrase + " BATERIA DEL EQUIPO";
        var segunda = "Engrase los 4 puntos del brazo cada 50 horas con grasa NLGI 2 y limpie las boquillas. " +
                      "BATERIA DEL EQUIPO La bateria es de 12 V. Consulte al servicio tecnico autorizado ante cualquier falla.";
        var fuentes = new List<FuenteDocumento> { new(1, 1, primera, "m.pdf"), new(2, 1, segunda, "m.pdf") };

        var secciones = SeccionesCandidatas.Armar(fuentes);

        var bateria = secciones.Single(seccion => seccion.Titulo == "BATERIA DEL EQUIPO");
        Assert.That(bateria.Cuerpo, Does.StartWith("La bateria es de 12 V").And.Not.Contain("Engrase"));
        Assert.That(secciones.Count(seccion => seccion.Cuerpo.StartsWith("Engrase los 4 puntos")), Is.EqualTo(1),
            "el texto repetido por el solapamiento no debe quedar como otra sección");
    }

    [Test]
    public void CompletarConFragmentoSiguiente_UneLaContinuacionSinTitulo()
    {
        var seccion = Seccion("CAMBIO DE ACEITE", "Drene el aceite usado y", llegaAlFinal: true);
        var siguientes = new List<SeccionDocumento> { Seccion("", "reemplace el filtro."), Seccion("LIMPIEZA", "Lave.") };

        var completa = SeccionesCandidatas.CompletarConFragmentoSiguiente(seccion, siguientes);

        Assert.That(completa.Cuerpo, Is.EqualTo("Drene el aceite usado y reemplace el filtro."));
        Assert.That(completa.Titulo, Is.EqualTo("CAMBIO DE ACEITE"));
    }

    [Test]
    public void CompletarConFragmentoSiguiente_TituloRepetidoUsaElCuerpoMasLargo()
    {
        var seccion = Seccion("BATERIA", "La bateria es", llegaAlFinal: true);
        var siguientes = new List<SeccionDocumento>
        {
            Seccion("", "Engrase los puntos del brazo."),
            Seccion("BATERIA", "La bateria es de 12 V. Consulte al servicio tecnico.")
        };

        var completa = SeccionesCandidatas.CompletarConFragmentoSiguiente(seccion, siguientes);

        Assert.That(completa.Cuerpo, Is.EqualTo("La bateria es de 12 V. Consulte al servicio tecnico."));
    }

    [Test]
    public void CompletarConFragmentoSiguiente_SiguienteConOtroTituloNoCambiaNada()
    {
        var seccion = Seccion("CAMBIO DE ACEITE", "Drene el aceite.", llegaAlFinal: true);

        var completa = SeccionesCandidatas.CompletarConFragmentoSiguiente(seccion, [Seccion("LIMPIEZA", "Lave.")]);

        Assert.That(completa, Is.SameAs(seccion));
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
    public void Candidatas_BuscarOpciones_PrimeroLaQueTieneMasPalabrasDeLaConsultaEnElTitulo()
    {
        var filtroHidraulico = Seccion("4.2 Cambio del filtro de aceite hidraulico", "a", id: 1);
        var aceiteDelMotor = Seccion("4.3 Cambio de aceite del motor", "b", id: 2);

        var opciones = SeccionesCandidatas.BuscarOpcionesEquivalentes("Cambio de aceite del motor",
            [filtroHidraulico, aceiteDelMotor]);

        Assert.That(opciones, Is.EqualTo(new[] { aceiteDelMotor, filtroHidraulico }));
    }

    [Test]
    public void Candidatas_BuscarOpciones_ConIgualCoincidenciaRespetaElOrdenDelReranker()
    {
        var primera = Seccion("4.2 Cambio del filtro de aceite hidraulico", "a", id: 1);
        var segunda = Seccion("4.3 Cambio de aceite del motor", "b", id: 2);

        var opciones = SeccionesCandidatas.BuscarOpcionesEquivalentes("cambio de aceite", [primera, segunda]);

        Assert.That(opciones, Is.EqualTo(new[] { primera, segunda }));
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
    public void Redaccion_CrearMensajeParaElModelo_IncluyeConsultaTituloContextoYTexto()
    {
        var seccion = Seccion("CAMBIO DE ACEITE", "cuerpo", contexto: "Contexto previo.");

        var mensaje = RedaccionDeSugerencia.CrearMensajeParaElModelo("¿cómo cambio el aceite?", seccion, "Drene " + TextoDelManual.MarcaDeSubtitulo + " el aceite.");

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
    public void Redaccion_CrearMensajeParaElModelo_SinContextoNoLoMenciona()
    {
        var mensaje = RedaccionDeSugerencia.CrearMensajeParaElModelo("q", Seccion("T", "c"), "texto");

        Assert.That(mensaje, Does.Not.Contain("Contexto previo"));
    }

    [Test]
    public void Redaccion_CrearMensajeParaElModelo_RecortaElTextoA3000()
    {
        var mensaje = RedaccionDeSugerencia.CrearMensajeParaElModelo("q", Seccion("T", "c"), new string('x', 5000));

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
    public void Redaccion_ArmarRespuesta_ConTituloDeLaIaReemplazaElContextoYLaFuenteConservaElOriginal()
    {
        var elegida = Seccion("500 HOURS OF SERVICE", "c", contexto: "Hours of service. 1. Park the machine.");

        var respuesta = RedaccionDeSugerencia.ArmarRespuesta("Resumen.", [], elegida, [elegida],
            "Servicio de mantenimiento de las 500 horas");

        Assert.That(respuesta, Does.StartWith("Servicio de mantenimiento de las 500 horas\nResumen.\n"));
        Assert.That(respuesta, Does.Not.Contain("Park the machine"));
        Assert.That(respuesta, Does.EndWith("sección \"500 Hours of service\"."));
    }

    [TestCase("Título: Cambio del filtro de combustible\nSe debe drenar el agua.",
        "Cambio del filtro de combustible", "Se debe drenar el agua.", TestName = "SepararTitulo_ConTitulo")]
    [TestCase("**Título:** Cambio del filtro.\n\nSe debe drenar el agua.",
        "Cambio del filtro", "Se debe drenar el agua.", TestName = "SepararTitulo_ConFormatoYPuntoFinal")]
    [TestCase("Título: Mantenimiento tras 250 horas\n**Resumen:** Se debe efectuar el mantenimiento.",
        "Mantenimiento tras 250 horas", "Se debe efectuar el mantenimiento.", TestName = "SepararTitulo_ConRotuloDeResumen")]
    [TestCase("Titulo: Cambio del filtro\nSe debe drenar el agua.",
        "Cambio del filtro", "Se debe drenar el agua.", TestName = "SepararTitulo_SinTilde")]
    [TestCase("Se debe drenar el agua del filtro.", "", "Se debe drenar el agua del filtro.",
        TestName = "SepararTitulo_SinTitulo")]
    [TestCase("Título: Cambio del filtro", "", "Título: Cambio del filtro", TestName = "SepararTitulo_SoloElTitulo")]
    [TestCase("Título: uno dos tres cuatro cinco seis siete ocho nueve diez once doce trece\nResumen del manual.",
        "", "Resumen del manual.", TestName = "SepararTitulo_TituloDemasiadoLargo")]
    public void Redaccion_SepararTitulo(string respuesta, string tituloEsperado, string resumenEsperado)
    {
        var separada = RedaccionDeSugerencia.SepararTitulo(respuesta);

        Assert.That(separada.Titulo, Is.EqualTo(tituloEsperado));
        Assert.That(separada.Resumen, Is.EqualTo(resumenEsperado));
    }

    [Test]
    public void Redaccion_EsResumenConfiable_UnNumeroInventadoEnElTituloLoDescarta()
    {
        var seccion = Seccion("Cambio de aceite", "Cada 250 horas cambie el aceite.");

        Assert.That(RedaccionDeSugerencia.EsResumenConfiable("Cada 250 horas se cambia el aceite.", "aceite", seccion,
            "Servicio de 250 horas"), Is.True);
        Assert.That(RedaccionDeSugerencia.EsResumenConfiable("Cada 250 horas se cambia el aceite.", "aceite", seccion,
            "Servicio de 500 horas"), Is.False);
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
    public void Redaccion_Instrucciones_ProhibenInventarDatos()
    {
        Assert.That(RedaccionDeSugerencia.Instrucciones, Does.Contain("No agregues pasos"));
        Assert.That(RedaccionDeSugerencia.Instrucciones, Does.Contain("Copiá exactamente los números"));
    }

    [Test]
    public void Redaccion_Instrucciones_ConservanLasObligaciones()
    {
        Assert.That(RedaccionDeSugerencia.Instrucciones, Does.Contain("Conservá las obligaciones y prohibiciones"));
    }

    [Test]
    public void Redaccion_Instrucciones_PidenTitulo_YTraduccionTecnica()
    {
        Assert.That(RedaccionDeSugerencia.Instrucciones, Does.Contain("En la primera línea escribí \"Título:\""));
        Assert.That(RedaccionDeSugerencia.Instrucciones, Does.Contain("vocabulario técnico de mantenimiento"));
        Assert.That(RedaccionDeSugerencia.Instrucciones, Does.Contain("el original entre paréntesis"));
    }
}
