using Api.DTOs.Activos;
using Api.DTOs.Clientes;
using Api.DTOs.ModelosMaquina;
using Api.DTOs.Repuestos;
using Api.DTOs.TiposMaquina;
using Api.DTOs.Usuarios;
using Api.Mapeadores;
using SOTRA_BACKEND.Modelos;

namespace Api.PruebasUnitarias;

public class MapeadoresPruebas
{
    // Cliente

    [Test]
    public void Cliente_AEntidad_NormalizaOpcionalesEnBlancoANulo()
    {
        var entidad = ClienteMapeador.AEntidad(new ClienteCrearDto
        {
            RazonSocial = "Acme",
            NombreContacto = "Ana",
            Correo = "  ",
            Telefono = "",
            Direccion = "\t",
            Observaciones = " ",
        });

        Assert.Multiple(() =>
        {
            Assert.That(entidad.Correo, Is.Null);
            Assert.That(entidad.Telefono, Is.Null);
            Assert.That(entidad.Direccion, Is.Null);
            Assert.That(entidad.Observaciones, Is.Null);
            Assert.That(entidad.Estado, Is.True);
            Assert.That(entidad.RazonSocial, Is.EqualTo("Acme"));
            Assert.That(entidad.Nombre, Is.EqualTo("Ana"));
            Assert.That(entidad.FechaIngreso, Is.InRange(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))));
        });
    }

    [Test]
    public void Cliente_AEntidad_ConservaOpcionalesConValor()
    {
        var entidad = ClienteMapeador.AEntidad(new ClienteCrearDto
        {
            RazonSocial = "Acme",
            NombreContacto = "Ana",
            Correo = "a@b.com",
            Telefono = "099",
            Direccion = "Calle 1",
            Observaciones = "Nota",
        });

        Assert.Multiple(() =>
        {
            Assert.That(entidad.Correo, Is.EqualTo("a@b.com"));
            Assert.That(entidad.Telefono, Is.EqualTo("099"));
            Assert.That(entidad.Direccion, Is.EqualTo("Calle 1"));
            Assert.That(entidad.Observaciones, Is.EqualTo("Nota"));
        });
    }

    [Test]
    public void Cliente_AplicarCambios_VaciaLosOpcionalesEnBlanco()
    {
        var cliente = new Cliente { RazonSocial = "x", Nombre = "y", Correo = "a@b.com", Telefono = "1", Direccion = "d", Observaciones = "o" };

        ClienteMapeador.AplicarCambios(cliente, new ClienteActualizarDto
        {
            RazonSocial = "Nueva",
            NombreContacto = "Beto",
            Correo = " ",
            Telefono = null,
            Direccion = "",
            Observaciones = "  ",
        });

        Assert.Multiple(() =>
        {
            Assert.That(cliente.RazonSocial, Is.EqualTo("Nueva"));
            Assert.That(cliente.Nombre, Is.EqualTo("Beto"));
            Assert.That(cliente.Correo, Is.Null);
            Assert.That(cliente.Telefono, Is.Null);
            Assert.That(cliente.Direccion, Is.Null);
            Assert.That(cliente.Observaciones, Is.Null);
        });
    }

    [TestCase(true, "activo")]
    [TestCase(false, "inactivo")]
    public void Cliente_AListadoDto_TraduceEstadoYCuentaActivos(bool estado, string esperado)
    {
        var cliente = new Cliente
        {
            IdCliente = 7,
            RazonSocial = "Acme",
            Nombre = "Ana",
            Estado = estado,
            FechaIngreso = new DateOnly(2025, 3, 4),
            Activos = [new Activo(), new Activo()],
        };

        var dto = ClienteMapeador.AListadoDto(cliente);

        Assert.Multiple(() =>
        {
            Assert.That(dto.Id, Is.EqualTo(7));
            Assert.That(dto.Estado, Is.EqualTo(esperado));
            Assert.That(dto.CantidadActivosAsociados, Is.EqualTo(2));
            Assert.That(dto.CreadoEn, Is.EqualTo(new DateOnly(2025, 3, 4)));
        });
    }

    // Activo

    private static Activo ActivoBase() => new()
    {
        IdActivo = 1,
        IdCliente = 2,
        NumSerie = "SN-1",
        Descripcion = "desc",
        IdModeloMaquina = 3,
        AnioFabricacion = 2020,
        Nombre = "Torno",
        Ubicacion = "Planta",
        Estado = true,
    };

    [Test]
    public void Activo_AEntidad_CopiaCamposYQuedaActivo()
    {
        var entidad = ActivoMapeador.AEntidad(new ActivoCrearDto
        {
            ClienteId = 9,
            NumSerie = "SN-9",
            ModeloMaquinaId = 4,
            AnioFabricacion = 1999,
            Nombre = "n",
            Ubicacion = "u",
            Descripcion = "d",
        });

        Assert.Multiple(() =>
        {
            Assert.That(entidad.IdCliente, Is.EqualTo(9));
            Assert.That(entidad.IdModeloMaquina, Is.EqualTo(4));
            Assert.That(entidad.NumSerie, Is.EqualTo("SN-9"));
            Assert.That(entidad.AnioFabricacion, Is.EqualTo((short)1999));
            Assert.That(entidad.Estado, Is.True);
        });
    }

    [Test]
    public void Activo_AplicarCambios_SinObligatoriosRechazaSinModificar()
    {
        var activo = ActivoBase();

        Assert.Throws<ArgumentException>(() => ActivoMapeador.AplicarCambios(activo, new ActivoActualizarDto()));

        Assert.Multiple(() =>
        {
            Assert.That(activo.IdCliente, Is.EqualTo(2));
            Assert.That(activo.NumSerie, Is.EqualTo("SN-1"));
            Assert.That(activo.Nombre, Is.EqualTo("Torno"));
            Assert.That(activo.Estado, Is.True);
        });
    }

    [Test]
    public void Activo_AplicarCambios_VaciaOpcionalesYAplicaLosDemas()
    {
        var activo = ActivoBase();

        ActivoMapeador.AplicarCambios(activo, new ActivoActualizarDto
        {
            ClienteId = 20,
            NumSerie = "SN-2",
            Nombre = "  ",
            Descripcion = null,
            Ubicacion = "",
            ModeloMaquinaId = 30,
            AnioFabricacion = null,
            Estado = "inactivo",
        });

        Assert.Multiple(() =>
        {
            Assert.That(activo.IdCliente, Is.EqualTo(20));
            Assert.That(activo.NumSerie, Is.EqualTo("SN-2"));
            Assert.That(activo.Nombre, Is.Null);
            Assert.That(activo.Descripcion, Is.Null);
            Assert.That(activo.Ubicacion, Is.Null);
            Assert.That(activo.IdModeloMaquina, Is.EqualTo(30));
            Assert.That(activo.AnioFabricacion, Is.Null);
            Assert.That(activo.Estado, Is.False);
        });
    }

    [Test]
    public void Activo_AplicarCambios_EstadoActivoReactiva()
    {
        var activo = ActivoBase();
        activo.Estado = false;

        ActivoMapeador.AplicarCambios(activo, new ActivoActualizarDto
        {
            ClienteId = 2,
            ModeloMaquinaId = 3,
            NumSerie = "SN-1",
            Estado = "activo",
        });

        Assert.That(activo.Estado, Is.True);
    }

    [TestCase("ACTIVO")]
    [TestCase("pausado")]
    [TestCase("")]
    public void Activo_AplicarCambios_EstadoInvalidoLanzaArgumentException(string estado)
    {
        Assert.Throws<ArgumentException>(() =>
            ActivoMapeador.AplicarCambios(ActivoBase(), new ActivoActualizarDto { Estado = estado }));
    }

    [Test]
    public void Activo_ADto_ToleraNavegacionesAusentes()
    {
        var dto = ActivoMapeador.ADto(ActivoBase());

        Assert.Multiple(() =>
        {
            Assert.That(dto.NombreCliente, Is.Null);
            Assert.That(dto.NombreModeloMaquina, Is.Null);
            Assert.That(dto.Estado, Is.EqualTo("activo"));
            Assert.That(dto.ClienteId, Is.EqualTo(2));
        });
    }

    [Test]
    public void Activo_ADto_IncluyeNombresDeNavegaciones()
    {
        var activo = ActivoBase();
        activo.IdClienteNavigation = new Cliente { RazonSocial = "Acme", Nombre = "x" };
        activo.IdModeloMaquinaNavigation = new ModeloMaquina { Nombre = "M1" };
        activo.Estado = false;

        var dto = ActivoMapeador.ADto(activo);

        Assert.Multiple(() =>
        {
            Assert.That(dto.NombreCliente, Is.EqualTo("Acme"));
            Assert.That(dto.NombreModeloMaquina, Is.EqualTo("M1"));
            Assert.That(dto.Estado, Is.EqualTo("inactivo"));
        });
    }

    // Tipos y modelos de máquina

    [Test]
    public void TipoMaquina_AplicarCambios_EstadoNuloNoCambiaElEstado()
    {
        var tipo = new TipoMaquina { Nombre = "a", Estado = true };

        TipoMaquinaMapeador.AplicarCambios(tipo, new TipoMaquinaActualizarDto { Nombre = "b" });

        Assert.That(tipo.Nombre, Is.EqualTo("b"));
        Assert.That(tipo.Estado, Is.True);
    }

    [Test]
    public void TipoMaquina_AplicarCambios_DesactivaYRechazaInvalidos()
    {
        var tipo = new TipoMaquina { Nombre = "a", Estado = true };

        TipoMaquinaMapeador.AplicarCambios(tipo, new TipoMaquinaActualizarDto { Nombre = "a", Estado = "inactivo" });

        Assert.That(tipo.Estado, Is.False);
        Assert.Throws<ArgumentException>(() =>
            TipoMaquinaMapeador.AplicarCambios(tipo, new TipoMaquinaActualizarDto { Nombre = "a", Estado = "x" }));
    }

    [Test]
    public void TipoMaquina_AEntidadYADto()
    {
        var entidad = TipoMaquinaMapeador.AEntidad(new TipoMaquinaCrearDto { Nombre = "Torno" });
        entidad.IdTipoMaquina = 5;

        var dto = TipoMaquinaMapeador.ADto(entidad);

        Assert.That(entidad.Estado, Is.True);
        Assert.That(dto.Id, Is.EqualTo(5));
        Assert.That(dto.Nombre, Is.EqualTo("Torno"));
        Assert.That(dto.Estado, Is.EqualTo("activo"));
    }

    [Test]
    public void ModeloMaquina_AplicarCambios_DescripcionEnBlancoPasaANulo()
    {
        var modelo = new ModeloMaquina { Nombre = "a", Descripcion = "algo", Activo = true };

        ModeloMaquinaMapeador.AplicarCambios(modelo, new ModeloMaquinaActualizarDto
        {
            Nombre = "b",
            TipoMaquinaId = 2,
            Descripcion = "   ",
        });

        Assert.Multiple(() =>
        {
            Assert.That(modelo.Descripcion, Is.Null);
            Assert.That(modelo.IdTipoMaquina, Is.EqualTo(2));
            Assert.That(modelo.Activo, Is.True);
        });
    }

    [Test]
    public void ModeloMaquina_AplicarCambios_EstadoYErrores()
    {
        var modelo = new ModeloMaquina { Nombre = "a", Activo = true };

        ModeloMaquinaMapeador.AplicarCambios(modelo,
            new ModeloMaquinaActualizarDto { Nombre = "a", TipoMaquinaId = 1, Estado = "inactivo" });

        Assert.That(modelo.Activo, Is.False);
        Assert.Throws<ArgumentException>(() => ModeloMaquinaMapeador.AplicarCambios(modelo,
            new ModeloMaquinaActualizarDto { Nombre = "a", TipoMaquinaId = 1, Estado = "?" }));
    }

    [Test]
    public void ModeloMaquina_ADto_ToleraTipoAusente()
    {
        var modelo = ModeloMaquinaMapeador.AEntidad(new ModeloMaquinaCrearDto { Nombre = "M", TipoMaquinaId = 4 });

        var dto = ModeloMaquinaMapeador.ADto(modelo);

        Assert.That(modelo.Activo, Is.True);
        Assert.That(dto.NombreTipoMaquina, Is.Null);
        Assert.That(dto.TipoMaquinaId, Is.EqualTo(4));
        Assert.That(dto.Estado, Is.EqualTo("activo"));
    }

    // Repuesto

    [Test]
    public void Repuesto_AEntidad_RecortaCodigoYNombre()
    {
        var entidad = RepuestoMapeador.AEntidad(new RepuestoCrearDto
        {
            Codigo = "  R-1 ",
            Nombre = " Filtro  ",
            Cantidad = 3,
            CantidadMinima = 5,
        });

        Assert.Multiple(() =>
        {
            Assert.That(entidad.Codigo, Is.EqualTo("R-1"));
            Assert.That(entidad.Nombre, Is.EqualTo("Filtro"));
            Assert.That(entidad.Estado, Is.True);
            Assert.That(entidad.Cantidad, Is.EqualTo(3));
            Assert.That(entidad.CantMin, Is.EqualTo(5));
        });
    }

    [Test]
    public void Repuesto_AplicarCambios_NoModificaLaCantidad()
    {
        var repuesto = new Repuesto { Codigo = "a", Nombre = "b", Cantidad = 10 };

        RepuestoMapeador.AplicarCambios(repuesto, new RepuestoActualizarDto
        {
            Codigo = " C ",
            Nombre = " N ",
            CantidadMinima = 2,
            Marca = "M",
        });

        Assert.Multiple(() =>
        {
            Assert.That(repuesto.Codigo, Is.EqualTo("C"));
            Assert.That(repuesto.Nombre, Is.EqualTo("N"));
            Assert.That(repuesto.Cantidad, Is.EqualTo(10));
            Assert.That(repuesto.CantMin, Is.EqualTo(2));
            Assert.That(repuesto.Marca, Is.EqualTo("M"));
        });
    }

    [TestCase(0, null, true, "sin_stock", "activo")]
    [TestCase(2, 5, true, "bajo", "activo")]
    [TestCase(9, 5, false, "normal", "inactivo")]
    public void Repuesto_ADto_CalculaEstadoDeStock(int cantidad, int? minimo, bool activo, string estadoStock, string estado)
    {
        var dto = RepuestoMapeador.ADto(new Repuesto
        {
            IdRepuesto = 4,
            Codigo = "c",
            Nombre = "n",
            Cantidad = cantidad,
            CantMin = minimo,
            Estado = activo,
        });

        Assert.Multiple(() =>
        {
            Assert.That(dto.EstadoStock, Is.EqualTo(estadoStock));
            Assert.That(dto.Estado, Is.EqualTo(estado));
            Assert.That(dto.CantidadMinima, Is.EqualTo(minimo ?? 0));
            Assert.That(dto.Descripcion, Is.EqualTo(""));
            Assert.That(dto.Marca, Is.EqualTo(""));
            Assert.That(dto.Ubicacion, Is.EqualTo(""));
        });
    }

    // Usuario y rol

    [Test]
    public void Usuario_AEntidad_CopiaCamposYQuedaActivo()
    {
        var entidad = UsuarioMapeador.AEntidad(new UsuarioCrearDto
        {
            Nombre = "Ana",
            Apellido = "Paz",
            Correo = "a@b.com",
            Telefono = "099",
        });

        Assert.Multiple(() =>
        {
            Assert.That(entidad.Mail, Is.EqualTo("a@b.com"));
            Assert.That(entidad.Estado, Is.True);
            Assert.That(entidad.FechaIngreso, Is.InRange(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))));
        });
    }

    [Test]
    public void Usuario_AplicarCambios_ActualizaYValidaEstado()
    {
        var usuario = new Usuario { Nombre = "a", Apellido = "b", Mail = "c", Telefono = "1", Estado = true };
        var cambios = new UsuarioActualizarDto
        {
            Nombre = "N",
            Apellido = "A",
            Correo = "n@a.com",
            Telefono = "2",
            IdsRol = [1],
            Estado = "inactivo",
            FechaIngreso = new DateOnly(2024, 1, 2),
        };

        UsuarioMapeador.AplicarCambios(usuario, cambios);

        Assert.Multiple(() =>
        {
            Assert.That(usuario.Nombre, Is.EqualTo("N"));
            Assert.That(usuario.Mail, Is.EqualTo("n@a.com"));
            Assert.That(usuario.Estado, Is.False);
            Assert.That(usuario.FechaIngreso, Is.EqualTo(new DateOnly(2024, 1, 2)));
        });
    }

    [Test]
    public void Usuario_AplicarCambios_EstadoInvalidoLanza()
    {
        var usuario = new Usuario { Nombre = "a", Apellido = "b", Mail = "c", Telefono = "1" };

        Assert.Throws<ArgumentException>(() => UsuarioMapeador.AplicarCambios(usuario, new UsuarioActualizarDto
        {
            Nombre = "N",
            Apellido = "A",
            Correo = "n@a.com",
            Telefono = "2",
            IdsRol = [],
            Estado = "otro",
            FechaIngreso = new DateOnly(2024, 1, 2),
        }));
    }

    [Test]
    public void Usuario_AListadoDto_OrdenaRolesPorId()
    {
        var usuario = new Usuario
        {
            IdUsuario = 1,
            Nombre = "a",
            Apellido = "b",
            Mail = "c",
            Telefono = "1",
            Estado = true,
            IdRols =
            [
                new Rol { IdRol = 3, Nombre = "tres", Activo = true },
                new Rol { IdRol = 1, Nombre = "uno", Activo = false },
            ],
        };

        var dto = UsuarioMapeador.AListadoDto(usuario);

        Assert.That(dto.Roles.Select(rol => rol.Id), Is.EqualTo(new long[] { 1, 3 }));
        Assert.That(dto.Roles[0].Activo, Is.False);
        Assert.That(dto.Estado, Is.EqualTo("activo"));
    }

    [Test]
    public void Usuario_AListadoDto_SinRoles()
    {
        var dto = UsuarioMapeador.AListadoDto(new Usuario { Nombre = "a", Apellido = "b", Mail = "c", Telefono = "1" });

        Assert.That(dto.Roles, Is.Empty);
        Assert.That(dto.Estado, Is.EqualTo("inactivo"));
    }

    [Test]
    public void Rol_ADto()
    {
        var dto = RolMapeador.ADto(new Rol { IdRol = 2, Nombre = "tecnico" });

        Assert.That(dto.Id, Is.EqualTo(2));
        Assert.That(dto.Nombre, Is.EqualTo("tecnico"));
    }

    // Movimiento de stock

    [Test]
    public void MovimientoStock_ADto_CompletaOpcionalesAusentes()
    {
        var dto = MovimientoStockMapeador.ADto(new MovimientoStock
        {
            IdMovimiento = 1,
            IdRepuesto = 2,
            Tipo = "entrada",
            Cantidad = 5,
            IdRepuestoNavigation = new Repuesto { Codigo = "R", Nombre = "Filtro" },
        });

        Assert.Multiple(() =>
        {
            Assert.That(dto.NombreUsuario, Is.Null);
            Assert.That(dto.Ubicacion, Is.EqualTo(""));
            Assert.That(dto.Motivo, Is.EqualTo(""));
            Assert.That(dto.Observaciones, Is.EqualTo(""));
            Assert.That(dto.StockAnterior, Is.Null);
            Assert.That(dto.CodigoRepuesto, Is.EqualTo("R"));
            Assert.That(dto.NombreRepuesto, Is.EqualTo("Filtro"));
        });
    }

    [Test]
    public void MovimientoStock_ADto_ConUsuarioYDatos()
    {
        var dto = MovimientoStockMapeador.ADto(new MovimientoStock
        {
            Tipo = "salida",
            Cantidad = 2,
            Motivo = "uso",
            Ubicacion = "A1",
            Observaciones = "obs",
            StockAnterior = 7,
            StockPosterior = 5,
            IdUsuario = 3,
            IdUsuarioNavigation = new Usuario { Nombre = "Ana", Apellido = "Paz", Mail = "", Telefono = "" },
            IdRepuestoNavigation = new Repuesto { Codigo = "R", Nombre = "F" },
        });

        Assert.Multiple(() =>
        {
            Assert.That(dto.NombreUsuario, Is.EqualTo("Ana Paz"));
            Assert.That(dto.Motivo, Is.EqualTo("uso"));
            Assert.That(dto.Ubicacion, Is.EqualTo("A1"));
            Assert.That(dto.StockAnterior, Is.EqualTo(7));
            Assert.That(dto.StockPosterior, Is.EqualTo(5));
        });
    }
}
