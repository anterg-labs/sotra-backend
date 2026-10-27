using Api.Mapeadores;
using SOTRA_BACKEND.Modelos;
using SOTRA_BACKEND.Operaciones;

namespace Api.PruebasUnitarias;

public class OrdenesTrabajoPruebas
{
    private static readonly DateTime Base = new(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc);
    private static readonly ReglasDeEstadoOrden Reglas = ReglasDePrueba.Documentadas();

    private static OrdenTrabajo CrearOrden(params (string Estado, int Minutos, string? Observacion)[] historial)
    {
        var orden = new OrdenTrabajo
        {
            IdOrden = 10,
            IdCliente = 1,
            IdActivo = 2,
            Tipo = false,
            Prioridad = 3,
            Descripcion = "Falla en el motor",
            FechaCreada = Base,
            IdClienteNavigation = new Cliente { RazonSocial = "Acme", Nombre = "Ana" },
            IdActivoNavigation = new Activo { NumSerie = "SN-1", Nombre = "Torno" },
        };

        var contador = 1;
        foreach (var (estado, minutos, observacion) in historial)
        {
            orden.HistorialEstadoOts.Add(new HistorialEstadoOt
            {
                IdHEstadoOt = contador++,
                FechaHora = Base.AddMinutes(minutos),
                Observacion = observacion,
                IdEstadoOtNavigation = new EstadoOt { Nombre = estado },
            });
        }

        return orden;
    }

    // Transiciones

    [TestCase("pendiente", new[] { "en_progreso", "eliminado" })]
    [TestCase("en_progreso", new[] { "en_revision", "pausado" })]
    [TestCase("en_revision", new[] { "pendiente", "cerrado" })]
    [TestCase("cerrado", new string[0])]
    [TestCase("pausado", new[] { "en_progreso", "eliminado" })]
    [TestCase("eliminado", new string[0])]
    public void EstadosSiguientes_SegunElCicloDeVida(string actual, string[] esperados)
    {
        Assert.That(Reglas.Siguientes(actual), Is.EqualTo(esperados));
    }

    [Test]
    public void EstadosDelCiclo_TodosLosEstadosSiguientesPertenecenAlCatalogo()
    {
        var nombres = Reglas.Estados.Select(estado => estado.Nombre).ToList();
        foreach (var estado in nombres)
        {
            Assert.That(nombres, Is.SupersetOf(Reglas.Siguientes(estado)), estado);
        }
    }

    [TestCase("en_progreso", "En progreso", "En progreso")]
    [TestCase("en_progreso", null, "En progreso")]
    [TestCase("en_revision", "  ", "En revision")]
    [TestCase("pausado", "", "Pausado")]
    public void EtiquetaDelEstado_UsaLaDeLaBaseYSiFaltaArmaUnaLegible(string nombre, string? descripcion, string esperada)
    {
        Assert.That(ReglasDeEstadoOrden.EtiquetaDelEstado(nombre, descripcion), Is.EqualTo(esperada));
    }

    [TestCase("desconocido")]
    [TestCase("")]
    [TestCase("PENDIENTE")]
    public void EstadosSiguientes_EstadoDesconocidoNoTieneSiguientes(string actual)
    {
        Assert.That(Reglas.Siguientes(actual), Is.Empty);
    }

    [Test]
    public void EstadosFinales_NoTienenSalida()
    {
        Assert.That(Reglas.Siguientes("cerrado"), Is.Empty);
        Assert.That(Reglas.Siguientes("eliminado"), Is.Empty);
    }

    private static void ValidarTransicion(string actual, string nuevo, long? tecnicoId = null, string? motivo = null)
    {
        Reglas.ValidarTransicion(actual, nuevo, tecnicoId, motivo);
    }

    private static string? CodigoDeError(string actual, string nuevo, long? tecnicoId = null, string? motivo = null)
    {
        try
        {
            ValidarTransicion(actual, nuevo, tecnicoId, motivo);
            return null;
        }
        catch (InvalidOperationException excepcion)
        {
            return excepcion.Message;
        }
    }

    [Test]
    public void ValidarTransicion_MismoEstadoEsNoOperacion()
    {
        Assert.That(CodigoDeError("cerrado", "cerrado"), Is.Null);
    }

    [TestCase("pendiente", "en_revision")]
    [TestCase("pendiente", "cerrado")]
    [TestCase("cerrado", "pendiente")]
    [TestCase("eliminado", "pendiente")]
    [TestCase("en_progreso", "cerrado")]
    [TestCase("inventado", "pendiente")]
    public void ValidarTransicion_RechazaSaltosNoPermitidos(string actual, string nuevo)
    {
        Assert.That(CodigoDeError(actual, nuevo, tecnicoId: 1, motivo: "m"), Is.EqualTo("TRANSICION_INVALIDA"));
    }

    [TestCase("pendiente", "en_progreso")]
    [TestCase("pausado", "en_progreso")]
    public void ValidarTransicion_IniciarOReanudarExigeTecnico(string actual, string nuevo)
    {
        Assert.That(CodigoDeError(actual, nuevo), Is.EqualTo("TECNICO_OBLIGATORIO"));
        Assert.That(CodigoDeError(actual, nuevo, tecnicoId: 5), Is.Null);
    }

    [TestCase("pendiente", "eliminado")]
    [TestCase("pausado", "eliminado")]
    [TestCase("en_progreso", "pausado")]
    public void ValidarTransicion_PausarOEliminarExigeMotivo(string actual, string nuevo)
    {
        Assert.That(CodigoDeError(actual, nuevo, tecnicoId: 1), Is.EqualTo("MOTIVO_OBLIGATORIO"));
        Assert.That(CodigoDeError(actual, nuevo, tecnicoId: 1, motivo: "   "), Is.EqualTo("MOTIVO_OBLIGATORIO"));
        Assert.That(CodigoDeError(actual, nuevo, tecnicoId: 1, motivo: "razón"), Is.Null);
    }

    [Test]
    public void ValidarTransicion_DevolverUnaOrdenEnRevisionAPendienteExigeMotivo()
    {
        Assert.That(CodigoDeError("en_revision", "pendiente"), Is.EqualTo("MOTIVO_OBLIGATORIO"));
        Assert.That(CodigoDeError("en_revision", "pendiente", motivo: "   "), Is.EqualTo("MOTIVO_OBLIGATORIO"));
        Assert.That(CodigoDeError("en_revision", "pendiente", motivo: "falló"), Is.Null);
    }

    [Test]
    public void ValidarTransicion_EnviarARevisionYCerrarNoExigenMotivoNiTecnico()
    {
        Assert.That(CodigoDeError("en_progreso", "en_revision"), Is.Null);
        Assert.That(CodigoDeError("en_revision", "cerrado"), Is.Null);
    }

    [Test]
    public void ValidarTransicion_LaTransicionInvalidaTienePrioridadSobreElTecnico()
    {
        Assert.That(CodigoDeError("cerrado", "en_progreso"), Is.EqualTo("TRANSICION_INVALIDA"));
    }

    // Conversión de tipo y prioridad

    [TestCase("preventivo", true)]
    [TestCase("correctivo", false)]
    public void ATipoBooleano(string tipo, bool esperado)
    {
        Assert.That(OrdenTrabajoMapeador.ATipoBooleano(tipo), Is.EqualTo(esperado));
    }

    [TestCase("")]
    [TestCase("Preventivo")]
    [TestCase("otro")]
    public void ATipoBooleano_InvalidoLanza(string tipo)
    {
        Assert.Throws<ArgumentException>(() => OrdenTrabajoMapeador.ATipoBooleano(tipo));
    }

    [TestCase("baja", 1)]
    [TestCase("media", 3)]
    [TestCase("alta", 5)]
    public void APrioridadNumero(string prioridad, short esperado)
    {
        Assert.That(OrdenTrabajoMapeador.APrioridadNumero(prioridad), Is.EqualTo(esperado));
    }

    [TestCase("urgente")]
    [TestCase("")]
    [TestCase("ALTA")]
    public void APrioridadNumero_InvalidaLanza(string prioridad)
    {
        Assert.Throws<ArgumentException>(() => OrdenTrabajoMapeador.APrioridadNumero(prioridad));
    }

    [TestCase(1, "baja")]
    [TestCase(2, "baja")]
    [TestCase(3, "media")]
    [TestCase(4, "media")]
    [TestCase(5, "alta")]
    [TestCase(9, "alta")]
    [TestCase(0, "baja")]
    public void ADto_TraduceLaPrioridadNumerica(short numero, string esperado)
    {
        var orden = CrearOrden(("pendiente", 0, null));
        orden.Prioridad = numero;

        Assert.That(OrdenTrabajoMapeador.ADto(orden, Reglas).Prioridad, Is.EqualTo(esperado));
    }

    // Mapeo completo

    [Test]
    public void ADto_SinHistorialQuedaPendienteConFechaPrevistaDeCreacion()
    {
        var dto = OrdenTrabajoMapeador.ADto(CrearOrden(), Reglas);

        Assert.Multiple(() =>
        {
            Assert.That(dto.Estado, Is.EqualTo("pendiente"));
            Assert.That(dto.FechaPrevistaEjecucion, Is.EqualTo("2026-01-10"));
            Assert.That(dto.Transiciones.Select(transicion => transicion.Estado), Is.EqualTo(new[] { "en_progreso", "eliminado" }));
            Assert.That(dto.HistorialEstados, Is.Empty);
            Assert.That(dto.Tipo, Is.EqualTo("correctivo"));
            Assert.That(dto.Observaciones, Is.EqualTo(""));
            Assert.That(dto.NombreTecnico, Is.Null);
        });
    }

    [Test]
    public void ADto_TomaElUltimoEstadoYLaFechaPrevistaDelPrimerHistorial()
    {
        var orden = CrearOrden(("pendiente", 0, "2026-02-15"), ("en_progreso", 10, "Se inició"), ("pausado", 20, "Falta de repuesto"));

        var dto = OrdenTrabajoMapeador.ADto(orden, Reglas);

        Assert.Multiple(() =>
        {
            Assert.That(dto.Estado, Is.EqualTo("pausado"));
            Assert.That(dto.FechaPrevistaEjecucion, Is.EqualTo("2026-02-15"));
            Assert.That(dto.Transiciones.Select(transicion => transicion.Estado), Is.EqualTo(new[] { "en_progreso", "eliminado" }));
            Assert.That(dto.HistorialEstados.Select(item => item.Estado),
                Is.EqualTo(new[] { "pendiente", "en_progreso", "pausado" }));
            Assert.That(dto.HistorialEstados[0].Observacion, Is.Null, "la fecha prevista no se muestra como observación");
            Assert.That(dto.HistorialEstados[1].Observacion, Is.EqualTo("Se inició"));
            Assert.That(dto.HistorialEstados[2].Observacion, Is.EqualTo("Falta de repuesto"));
        });
    }

    [Test]
    public void ADto_FechaPrevistaInvalidaEnElPrimerHistorialUsaLaDeCreacion()
    {
        var orden = CrearOrden(("pendiente", 0, "no es una fecha"));

        Assert.That(OrdenTrabajoMapeador.ADto(orden, Reglas).FechaPrevistaEjecucion, Is.EqualTo("2026-01-10"));
    }

    [Test]
    public void ADto_EmpateDeFechaSeDesempataPorIdDeHistorial()
    {
        var orden = CrearOrden(("pendiente", 0, null), ("en_progreso", 0, null));

        Assert.That(OrdenTrabajoMapeador.ADto(orden, Reglas).Estado, Is.EqualTo("en_progreso"));
    }

    [Test]
    public void ADto_EnElHistorialMuestraDeQueEstadoVinoQuienLoHizoYElMotivo()
    {
        var orden = CrearOrden(("pendiente", 0, "2026-02-15"), ("en_progreso", 10, null), ("pausado", 20, "Falta de repuesto"));
        var jefe = new Usuario { IdUsuario = 4, Nombre = "Ana", Apellido = "Pérez", Mail = "", Telefono = "" };
        var historial = orden.HistorialEstadoOts.OrderBy(item => item.FechaHora).ToList();
        historial[1].IdEstadoAnteriorNavigation = historial[0].IdEstadoOtNavigation;
        historial[1].IdUsuario = 4;
        historial[1].IdUsuarioNavigation = jefe;
        historial[2].IdEstadoAnteriorNavigation = historial[1].IdEstadoOtNavigation;
        historial[2].IdUsuario = 4;
        historial[2].IdUsuarioNavigation = jefe;

        var dto = OrdenTrabajoMapeador.ADto(orden, Reglas);

        Assert.Multiple(() =>
        {
            Assert.That(dto.HistorialEstados[0].EstadoAnterior, Is.Null, "el alta no viene de ningún estado");
            Assert.That(dto.HistorialEstados[0].NombreUsuario, Is.Null);
            Assert.That(dto.HistorialEstados[2].EstadoAnterior, Is.EqualTo("en_progreso"));
            Assert.That(dto.HistorialEstados[2].Estado, Is.EqualTo("pausado"));
            Assert.That(dto.HistorialEstados[2].Observacion, Is.EqualTo("Falta de repuesto"));
            Assert.That(dto.HistorialEstados[2].UsuarioId, Is.EqualTo(4));
            Assert.That(dto.HistorialEstados[2].NombreUsuario, Is.EqualTo("Ana Pérez"));
        });
    }

    [Test]
    public void ADto_EnElHistorialDeTecnicosMuestraLaReasignacionEnOrdenCronologico()
    {
        var orden = CrearOrden(("pendiente", 0, null));
        var anterior = new Usuario { IdUsuario = 8, Nombre = "Luis", Apellido = "Gómez", Mail = "", Telefono = "" };
        var nuevo = new Usuario { IdUsuario = 9, Nombre = "Marta", Apellido = "Ríos", Mail = "", Telefono = "" };
        orden.HistorialTecnicoOts.Add(new HistorialTecnicoOt
        {
            IdHTecnicoOt = 2,
            FechaHora = Base.AddMinutes(30),
            IdTecnicoAnterior = 8,
            IdTecnicoNuevo = 9,
            IdTecnicoAnteriorNavigation = anterior,
            IdTecnicoNuevoNavigation = nuevo,
        });
        orden.HistorialTecnicoOts.Add(new HistorialTecnicoOt
        {
            IdHTecnicoOt = 1,
            FechaHora = Base.AddMinutes(5),
            IdTecnicoNuevo = 8,
            IdTecnicoNuevoNavigation = anterior,
        });

        var tecnicos = OrdenTrabajoMapeador.ADto(orden, Reglas).HistorialTecnicos;

        Assert.Multiple(() =>
        {
            Assert.That(tecnicos.Select(item => item.TecnicoId), Is.EqualTo(new long?[] { 8, 9 }));
            Assert.That(tecnicos[0].TecnicoAnteriorId, Is.Null, "la primera asignación no tiene técnico anterior");
            Assert.That(tecnicos[1].TecnicoAnteriorId, Is.EqualTo(8));
            Assert.That(tecnicos[1].NombreTecnicoAnterior, Is.EqualTo("Luis Gómez"));
            Assert.That(tecnicos[1].NombreTecnico, Is.EqualTo("Marta Ríos"));
        });
    }

    [Test]
    public void ADto_UsaNumeroDeSerieSiElActivoNoTieneNombre()
    {
        var orden = CrearOrden(("pendiente", 0, null));
        orden.IdActivoNavigation.Nombre = "  ";

        Assert.That(OrdenTrabajoMapeador.ADto(orden, Reglas).NombreActivo, Is.EqualTo("SN-1"));
    }

    [Test]
    public void ADto_ArmaTecnicoTipoObservacionesRepuestosYSugerencias()
    {
        var orden = CrearOrden(("pendiente", 0, null));
        orden.Tipo = true;
        orden.Resultado = "Cambio de correa";
        orden.IdUsuarioTecnico = 8;
        orden.IdUsuarioTecnicoNavigation = new Usuario
        {
            Nombre = "Luis",
            Apellido = "Gómez",
            Mail = "",
            Telefono = "",
        };
        orden.OtRepuestos.Add(new OtRepuesto
        {
            IdOtRepuesto = 2,
            IdRepuestoNavigation = new Repuesto { IdRepuesto = 22, Codigo = "B", Nombre = "Segundo" },
        });
        orden.OtRepuestos.Add(new OtRepuesto
        {
            IdOtRepuesto = 1,
            IdRepuestoNavigation = new Repuesto { IdRepuesto = 11, Codigo = "A", Nombre = "Primero" },
        });
        orden.HistorialRecomendacionesIa.Add(new HistorialRecomendacionIa { IdRecomendacionIa = 2, Estado = true, Recomendacion = "r2" });
        orden.HistorialRecomendacionesIa.Add(new HistorialRecomendacionIa { IdRecomendacionIa = 1, Estado = true, Recomendacion = "r1" });
        orden.HistorialRecomendacionesIa.Add(new HistorialRecomendacionIa { IdRecomendacionIa = 3, Estado = false, Recomendacion = "descartada" });

        var dto = OrdenTrabajoMapeador.ADto(orden, Reglas);

        Assert.Multiple(() =>
        {
            Assert.That(dto.Tipo, Is.EqualTo("preventivo"));
            Assert.That(dto.NombreTecnico, Is.EqualTo("Luis Gómez"));
            Assert.That(dto.TecnicoId, Is.EqualTo(8));
            Assert.That(dto.Observaciones, Is.EqualTo("Cambio de correa"));
            Assert.That(dto.Repuestos.Select(repuesto => repuesto.Codigo), Is.EqualTo(new[] { "A", "B" }));
            Assert.That(dto.Sugerencias, Is.EqualTo(new[] { "r1", "r2" }));
            Assert.That(dto.NombreCliente, Is.EqualTo("Acme"));
        });
    }

    [Test]
    public void ADto_MuestraCantidadYEstadoDeCadaRepuestoYNoListaLosLiberados()
    {
        var orden = CrearOrden(("pendiente", 0, null));
        orden.OtRepuestos.Add(new OtRepuesto
        {
            IdOtRepuesto = 1,
            Cantidad = 3,
            Estado = "reservado",
            IdRepuestoNavigation = new Repuesto { IdRepuesto = 11, Codigo = "A", Nombre = "Primero" },
        });
        orden.OtRepuestos.Add(new OtRepuesto
        {
            IdOtRepuesto = 2,
            Cantidad = 5,
            Estado = "liberado",
            IdRepuestoNavigation = new Repuesto { IdRepuesto = 22, Codigo = "B", Nombre = "Segundo" },
        });

        var repuestos = OrdenTrabajoMapeador.ADto(orden, Reglas).Repuestos;

        Assert.That(repuestos, Has.Count.EqualTo(1));
        Assert.That((repuestos[0].Codigo, repuestos[0].Cantidad, repuestos[0].Estado), Is.EqualTo(("A", 3, "reservado")));
    }

    [Test]
    public void ADto_EnElHistorialDeRepuestosMuestraQueSeHizoConCuantasUnidadesQuienYCuando()
    {
        var orden = CrearOrden(("pendiente", 0, null));
        var jefe = new Usuario { IdUsuario = 4, Nombre = "Ana", Apellido = "Pérez", Mail = "", Telefono = "" };
        var repuesto = new Repuesto { IdRepuesto = 11, Codigo = "A", Nombre = "Primero" };
        orden.HistorialRepuestoOts.Add(new HistorialRepuestoOt
        {
            IdHRepuestoOt = 2,
            IdRepuesto = 11,
            IdRepuestoNavigation = repuesto,
            Accion = "modificar",
            CantidadAnterior = 4,
            CantidadNueva = 3,
            EstadoAnterior = "reservado",
            EstadoNuevo = "reservado",
            FechaHora = Base.AddMinutes(20),
            IdUsuario = 4,
            IdUsuarioNavigation = jefe,
        });
        orden.HistorialRepuestoOts.Add(new HistorialRepuestoOt
        {
            IdHRepuestoOt = 1,
            IdRepuesto = 11,
            IdRepuestoNavigation = repuesto,
            Accion = "reservar",
            CantidadNueva = 4,
            EstadoNuevo = "reservado",
            FechaHora = Base.AddMinutes(10),
        });

        var historial = OrdenTrabajoMapeador.ADto(orden, Reglas).HistorialRepuestos;

        Assert.That(historial.Select(item => item.Accion), Is.EqualTo(new[] { "reservar", "modificar" }));
        Assert.That(historial[0].NombreUsuario, Is.Null);
        Assert.That((historial[1].CantidadAnterior, historial[1].CantidadNueva, historial[1].NombreUsuario), Is.EqualTo(((int?)4, 3, "Ana Pérez")));
    }

    // Versión (ETag)

    [Test]
    public void Version_EsDeterministaYTieneDieciseisCaracteresHexadecimales()
    {
        var primera = OrdenTrabajoMapeador.ADto(CrearOrden(("pendiente", 0, null)), Reglas).Version;
        var segunda = OrdenTrabajoMapeador.ADto(CrearOrden(("pendiente", 0, null)), Reglas).Version;

        Assert.That(primera, Is.EqualTo(segunda));
        Assert.That(primera, Does.Match("^[0-9a-f]{16}$"));
    }

    [Test]
    public void Version_CambiaSiCambiaElContenido()
    {
        var original = OrdenTrabajoMapeador.ADto(CrearOrden(("pendiente", 0, null)), Reglas).Version;

        var descripcion = CrearOrden(("pendiente", 0, null));
        descripcion.Descripcion = "Otra descripción";
        var prioridad = CrearOrden(("pendiente", 0, null));
        prioridad.Prioridad = 5;
        var estado = CrearOrden(("pendiente", 0, null), ("en_progreso", 5, null));
        var resultado = CrearOrden(("pendiente", 0, null));
        resultado.Resultado = "listo";

        var versiones = new[]
        {
            original,
            OrdenTrabajoMapeador.ADto(descripcion, Reglas).Version,
            OrdenTrabajoMapeador.ADto(prioridad, Reglas).Version,
            OrdenTrabajoMapeador.ADto(estado, Reglas).Version,
            OrdenTrabajoMapeador.ADto(resultado, Reglas).Version,
        };

        Assert.That(versiones, Is.Unique);
    }

    [Test]
    public void Version_CambiaConUnNuevoRepuestoOSugerencia()
    {
        var original = OrdenTrabajoMapeador.ADto(CrearOrden(("pendiente", 0, null)), Reglas).Version;

        var conRepuesto = CrearOrden(("pendiente", 0, null));
        conRepuesto.OtRepuestos.Add(new OtRepuesto
        {
            IdOtRepuesto = 1,
            IdRepuestoNavigation = new Repuesto { IdRepuesto = 1, Codigo = "A", Nombre = "N" },
        });
        var conSugerencia = CrearOrden(("pendiente", 0, null));
        conSugerencia.HistorialRecomendacionesIa.Add(new HistorialRecomendacionIa { Estado = true, Recomendacion = "x" });

        Assert.That(OrdenTrabajoMapeador.ADto(conRepuesto, Reglas).Version, Is.Not.EqualTo(original));
        Assert.That(OrdenTrabajoMapeador.ADto(conSugerencia, Reglas).Version, Is.Not.EqualTo(original));
    }

    [Test]
    public void Version_NoDependeDelOrdenDeLasColeccionesDeEntrada()
    {
        var a = CrearOrden(("pendiente", 0, null), ("en_progreso", 5, null));
        var b = CrearOrden(("pendiente", 0, null), ("en_progreso", 5, null));
        var invertida = b.HistorialEstadoOts.Reverse().ToList();
        b.HistorialEstadoOts.Clear();
        foreach (var item in invertida)
        {
            b.HistorialEstadoOts.Add(item);
        }

        Assert.That(OrdenTrabajoMapeador.ADto(b, Reglas).Version, Is.EqualTo(OrdenTrabajoMapeador.ADto(a, Reglas).Version));
    }
}
