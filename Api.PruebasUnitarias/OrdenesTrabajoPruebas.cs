using System.Reflection;
using Api.Mapeadores;
using SOTRA_BACKEND.Modelos;
using SOTRA_BACKEND.Operaciones;

namespace Api.PruebasUnitarias;

public class OrdenesTrabajoPruebas
{
    private static readonly DateTime Base = new(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc);

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

    [TestCase("creada", new[] { "asignada", "cancelada" })]
    [TestCase("asignada", new[] { "en_ejecucion", "cancelada" })]
    [TestCase("en_ejecucion", new[] { "pausada", "finalizada" })]
    [TestCase("pausada", new[] { "en_ejecucion", "cancelada" })]
    [TestCase("finalizada", new[] { "cerrada", "asignada" })]
    [TestCase("cerrada", new string[0])]
    [TestCase("cancelada", new string[0])]
    public void EstadosSiguientes_SegunElCicloDeVida(string actual, string[] esperados)
    {
        Assert.That(OrdenTrabajoDAO.ObtenerEstadosSiguientes(actual), Is.EqualTo(esperados));
    }

    [TestCase("desconocido")]
    [TestCase("")]
    [TestCase("CREADA")]
    public void EstadosSiguientes_EstadoDesconocidoNoTieneSiguientes(string actual)
    {
        Assert.That(OrdenTrabajoDAO.ObtenerEstadosSiguientes(actual), Is.Empty);
    }

    [Test]
    public void EstadosSiguientes_DevuelveUnaListaNuevaCadaVez()
    {
        var primera = OrdenTrabajoDAO.ObtenerEstadosSiguientes("creada");
        primera.Add("cerrada");

        Assert.That(OrdenTrabajoDAO.ObtenerEstadosSiguientes("creada"), Is.EqualTo(new[] { "asignada", "cancelada" }));
    }

    [Test]
    public void EstadosFinales_NoTienenSalida()
    {
        Assert.That(OrdenTrabajoDAO.ObtenerEstadosSiguientes("cerrada"), Is.Empty);
        Assert.That(OrdenTrabajoDAO.ObtenerEstadosSiguientes("cancelada"), Is.Empty);
    }

    private static void ValidarTransicion(string actual, string nuevo, long? tecnicoId = null, string? motivo = null)
    {
        var metodo = typeof(OrdenTrabajoDAO).GetMethod("ValidarTransicion", BindingFlags.NonPublic | BindingFlags.Static)!;
        try
        {
            metodo.Invoke(null, [actual, nuevo, tecnicoId, motivo]);
        }
        catch (TargetInvocationException excepcion)
        {
            throw excepcion.InnerException!;
        }
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
        Assert.That(CodigoDeError("cerrada", "cerrada"), Is.Null);
    }

    [TestCase("creada", "en_ejecucion")]
    [TestCase("creada", "cerrada")]
    [TestCase("cerrada", "asignada")]
    [TestCase("cancelada", "creada")]
    [TestCase("en_ejecucion", "cerrada")]
    [TestCase("inventado", "asignada")]
    public void ValidarTransicion_RechazaSaltosNoPermitidos(string actual, string nuevo)
    {
        Assert.That(CodigoDeError(actual, nuevo, tecnicoId: 1, motivo: "m"), Is.EqualTo("TRANSICION_INVALIDA"));
    }

    [TestCase("creada", "asignada")]
    [TestCase("pausada", "en_ejecucion")]
    public void ValidarTransicion_AsignarOEjecutarExigeTecnico(string actual, string nuevo)
    {
        Assert.That(CodigoDeError(actual, nuevo), Is.EqualTo("TECNICO_OBLIGATORIO"));
        Assert.That(CodigoDeError(actual, nuevo, tecnicoId: 5), Is.Null);
    }

    [TestCase("creada", "cancelada")]
    [TestCase("en_ejecucion", "pausada")]
    [TestCase("finalizada", "cerrada")]
    public void ValidarTransicion_PausarCancelarOCerrarExigeMotivo(string actual, string nuevo)
    {
        Assert.That(CodigoDeError(actual, nuevo, tecnicoId: 1), Is.EqualTo("MOTIVO_OBLIGATORIO"));
        Assert.That(CodigoDeError(actual, nuevo, tecnicoId: 1, motivo: "   "), Is.EqualTo("MOTIVO_OBLIGATORIO"));
        Assert.That(CodigoDeError(actual, nuevo, tecnicoId: 1, motivo: "razón"), Is.Null);
    }

    [Test]
    public void ValidarTransicion_ReabrirUnaFinalizadaExigeMotivo()
    {
        Assert.That(CodigoDeError("finalizada", "asignada", tecnicoId: 1), Is.EqualTo("MOTIVO_OBLIGATORIO"));
        Assert.That(CodigoDeError("finalizada", "asignada", tecnicoId: 1, motivo: "falló"), Is.Null);
    }

    [Test]
    public void ValidarTransicion_FinalizarNoExigeMotivoNiTecnico()
    {
        Assert.That(CodigoDeError("en_ejecucion", "finalizada"), Is.Null);
    }

    [Test]
    public void ValidarTransicion_LaTransicionInvalidaTienePrioridadSobreElTecnico()
    {
        Assert.That(CodigoDeError("cerrada", "asignada"), Is.EqualTo("TRANSICION_INVALIDA"));
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
        var orden = CrearOrden(("creada", 0, null));
        orden.Prioridad = numero;

        Assert.That(OrdenTrabajoMapeador.ADto(orden).Prioridad, Is.EqualTo(esperado));
    }

    // Mapeo completo

    [Test]
    public void ADto_SinHistorialQuedaCreadaConFechaPrevistaDeCreacion()
    {
        var dto = OrdenTrabajoMapeador.ADto(CrearOrden());

        Assert.Multiple(() =>
        {
            Assert.That(dto.Estado, Is.EqualTo("creada"));
            Assert.That(dto.FechaPrevistaEjecucion, Is.EqualTo("2026-01-10"));
            Assert.That(dto.EstadosSiguientes, Is.EqualTo(new[] { "asignada", "cancelada" }));
            Assert.That(dto.HistorialEstados, Is.Empty);
            Assert.That(dto.Tipo, Is.EqualTo("correctivo"));
            Assert.That(dto.Observaciones, Is.EqualTo(""));
            Assert.That(dto.NombreTecnico, Is.Null);
        });
    }

    [Test]
    public void ADto_TomaElUltimoEstadoYLaFechaPrevistaDelPrimerHistorial()
    {
        var orden = CrearOrden(("creada", 0, "2026-02-15"), ("asignada", 10, "Se asignó"), ("en_ejecucion", 20, null));

        var dto = OrdenTrabajoMapeador.ADto(orden);

        Assert.Multiple(() =>
        {
            Assert.That(dto.Estado, Is.EqualTo("en_ejecucion"));
            Assert.That(dto.FechaPrevistaEjecucion, Is.EqualTo("2026-02-15"));
            Assert.That(dto.EstadosSiguientes, Is.EqualTo(new[] { "pausada", "finalizada" }));
            Assert.That(dto.HistorialEstados.Select(item => item.Estado),
                Is.EqualTo(new[] { "creada", "asignada", "en_ejecucion" }));
            Assert.That(dto.HistorialEstados[0].Observacion, Is.Null, "la fecha prevista no se muestra como observación");
            Assert.That(dto.HistorialEstados[1].Observacion, Is.EqualTo("Se asignó"));
        });
    }

    [Test]
    public void ADto_FechaPrevistaInvalidaEnElPrimerHistorialUsaLaDeCreacion()
    {
        var orden = CrearOrden(("creada", 0, "no es una fecha"));

        Assert.That(OrdenTrabajoMapeador.ADto(orden).FechaPrevistaEjecucion, Is.EqualTo("2026-01-10"));
    }

    [Test]
    public void ADto_EmpateDeFechaSeDesempataPorIdDeHistorial()
    {
        var orden = CrearOrden(("creada", 0, null), ("asignada", 0, null));

        Assert.That(OrdenTrabajoMapeador.ADto(orden).Estado, Is.EqualTo("asignada"));
    }

    [Test]
    public void ADto_UsaNumeroDeSerieSiElActivoNoTieneNombre()
    {
        var orden = CrearOrden(("creada", 0, null));
        orden.IdActivoNavigation.Nombre = "  ";

        Assert.That(OrdenTrabajoMapeador.ADto(orden).NombreActivo, Is.EqualTo("SN-1"));
    }

    [Test]
    public void ADto_ArmaTecnicoTipoObservacionesRepuestosYSugerencias()
    {
        var orden = CrearOrden(("creada", 0, null));
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

        var dto = OrdenTrabajoMapeador.ADto(orden);

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

    // Versión (ETag)

    [Test]
    public void Version_EsDeterministaYTieneDieciseisCaracteresHexadecimales()
    {
        var primera = OrdenTrabajoMapeador.ADto(CrearOrden(("creada", 0, null))).Version;
        var segunda = OrdenTrabajoMapeador.ADto(CrearOrden(("creada", 0, null))).Version;

        Assert.That(primera, Is.EqualTo(segunda));
        Assert.That(primera, Does.Match("^[0-9a-f]{16}$"));
    }

    [Test]
    public void Version_CambiaSiCambiaElContenido()
    {
        var original = OrdenTrabajoMapeador.ADto(CrearOrden(("creada", 0, null))).Version;

        var descripcion = CrearOrden(("creada", 0, null));
        descripcion.Descripcion = "Otra descripción";
        var prioridad = CrearOrden(("creada", 0, null));
        prioridad.Prioridad = 5;
        var estado = CrearOrden(("creada", 0, null), ("asignada", 5, null));
        var resultado = CrearOrden(("creada", 0, null));
        resultado.Resultado = "listo";

        var versiones = new[]
        {
            original,
            OrdenTrabajoMapeador.ADto(descripcion).Version,
            OrdenTrabajoMapeador.ADto(prioridad).Version,
            OrdenTrabajoMapeador.ADto(estado).Version,
            OrdenTrabajoMapeador.ADto(resultado).Version,
        };

        Assert.That(versiones, Is.Unique);
    }

    [Test]
    public void Version_CambiaConUnNuevoRepuestoOSugerencia()
    {
        var original = OrdenTrabajoMapeador.ADto(CrearOrden(("creada", 0, null))).Version;

        var conRepuesto = CrearOrden(("creada", 0, null));
        conRepuesto.OtRepuestos.Add(new OtRepuesto
        {
            IdOtRepuesto = 1,
            IdRepuestoNavigation = new Repuesto { IdRepuesto = 1, Codigo = "A", Nombre = "N" },
        });
        var conSugerencia = CrearOrden(("creada", 0, null));
        conSugerencia.HistorialRecomendacionesIa.Add(new HistorialRecomendacionIa { Estado = true, Recomendacion = "x" });

        Assert.That(OrdenTrabajoMapeador.ADto(conRepuesto).Version, Is.Not.EqualTo(original));
        Assert.That(OrdenTrabajoMapeador.ADto(conSugerencia).Version, Is.Not.EqualTo(original));
    }

    [Test]
    public void Version_NoDependeDelOrdenDeLasColeccionesDeEntrada()
    {
        var a = CrearOrden(("creada", 0, null), ("asignada", 5, null));
        var b = CrearOrden(("creada", 0, null), ("asignada", 5, null));
        var invertida = b.HistorialEstadoOts.Reverse().ToList();
        b.HistorialEstadoOts.Clear();
        foreach (var item in invertida)
        {
            b.HistorialEstadoOts.Add(item);
        }

        Assert.That(OrdenTrabajoMapeador.ADto(b).Version, Is.EqualTo(OrdenTrabajoMapeador.ADto(a).Version));
    }
}
