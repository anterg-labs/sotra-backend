using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Contexto;

public partial class SotraContext : DbContext
{
    private const string EsquemaOperativo = "operativo";
    private const string EsquemaVectorial = "vectorial";

    public SotraContext()
    {
    }

    public SotraContext(DbContextOptions<SotraContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Activo> Activos { get; set; }

    public virtual DbSet<Cliente> Clientes { get; set; }

    public virtual DbSet<EstadoOt> EstadoOts { get; set; }

    public virtual DbSet<TransicionEstadoOt> TransicionEstadoOts { get; set; }

    public virtual DbSet<HistorialEstadoOt> HistorialEstadoOts { get; set; }

    public virtual DbSet<HistorialTecnicoOt> HistorialTecnicoOts { get; set; }

    public virtual DbSet<HistorialRepuestoOt> HistorialRepuestoOts { get; set; }

    public virtual DbSet<HistorialLecturaUsoOt> HistorialLecturaUsoOts { get; set; }

    public virtual DbSet<CausaFalla> CausasFalla { get; set; }

    public virtual DbSet<ReglaAviso> ReglasAviso { get; set; }

    public virtual DbSet<HistorialRecomendacionIa> HistorialRecomendacionesIa { get; set; }

    public virtual DbSet<ModeloMaquina> ModeloMaquinas { get; set; }

    public virtual DbSet<MotivoEstadoOt> MotivoEstadoOts { get; set; }

    public virtual DbSet<MovimientoStock> MovimientoStocks { get; set; }

    public virtual DbSet<EvidenciaOt> EvidenciasOt { get; set; }

    public virtual DbSet<TerminosCondiciones> TerminosCondiciones { get; set; }

    public virtual DbSet<AceptacionTerminos> AceptacionesTerminos { get; set; }


    public virtual DbSet<OrdenTrabajo> OrdenTrabajos { get; set; }

    public virtual DbSet<OtRepuesto> OtRepuestos { get; set; }

    public virtual DbSet<Permiso> Permisos { get; set; }

    public virtual DbSet<Repuesto> Repuestos { get; set; }

    public virtual DbSet<Rol> Rols { get; set; }

    public virtual DbSet<TipoMaquina> TipoMaquinas { get; set; }

    public virtual DbSet<Usuario> Usuarios { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            DotNetEnv.Env.NoClobber().TraversePath().Load();

            var host = Environment.GetEnvironmentVariable("DB_HOST");
            var port = Environment.GetEnvironmentVariable("DB_PORT");
            var database = Environment.GetEnvironmentVariable("DB_NAME");
            var username = Environment.GetEnvironmentVariable("DB_USER");
            var password = Environment.GetEnvironmentVariable("DB_PASSWORD");

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(database)
                || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "No se encontraron las variables de conexión en el archivo .env (DB_HOST, DB_NAME, DB_USER, DB_PASSWORD).");
            }

            var connectionString =
                $"Host={host};Port={port ?? "5432"};Database={database};Username={username};Password={password}";

            optionsBuilder.UseNpgsql(connectionString);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Activo>(entity =>
        {
            entity.HasKey(e => e.IdActivo).HasName("PK_ACTIVO");

            entity.ToTable("activo", EsquemaOperativo);

            entity.HasIndex(e => e.IdModeloMaquina, "IX_ACTIVO_ID_MODELO_MAQUINA");

            entity.HasIndex(e => e.NumSerie, "UK_NUM_SERIE_ACTIVO").IsUnique();

            entity.Property(e => e.IdActivo)
                .HasDefaultValueSql("nextval('" + EsquemaOperativo + ".\"SEC_ACTIVO_ID\"'::regclass)")
                .HasColumnName("id_activo");
            entity.Property(e => e.Estado)
                .HasDefaultValue(true)
                .HasColumnName("estado");
            entity.Property(e => e.AnioFabricacion).HasColumnName("anio_fabricacion");
            entity.Property(e => e.UnidadUso).HasMaxLength(10).HasDefaultValue("horas").HasColumnName("unidad_uso");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.IdCliente).HasColumnName("id_cliente");
            entity.Property(e => e.IdModeloMaquina).HasColumnName("id_modelo_maquina");
            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .HasColumnName("nombre");
            entity.Property(e => e.NumSerie)
                .HasMaxLength(100)
                .HasColumnName("num_serie");
            entity.Property(e => e.Ubicacion)
                .HasMaxLength(150)
                .HasColumnName("ubicacion");

            entity.HasOne(d => d.IdClienteNavigation).WithMany(p => p.Activos)
                .HasForeignKey(d => d.IdCliente)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CLIENTE_ACTIVO");

            entity.HasOne(d => d.IdModeloMaquinaNavigation).WithMany(p => p.Activos)
                .HasForeignKey(d => d.IdModeloMaquina)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MODELO_MAQUINA_ACTIVO");
        });

        modelBuilder.Entity<CausaFalla>(entity =>
        {
            entity.HasKey(e => e.IdCausaFalla).HasName("PK_CAUSA_FALLA");

            entity.ToTable("causa_falla", EsquemaOperativo);

            entity.Property(e => e.IdCausaFalla).ValueGeneratedOnAdd().HasColumnName("id_causa_falla");
            entity.Property(e => e.Nombre).HasMaxLength(80).HasColumnName("nombre");
            entity.Property(e => e.Estado).HasDefaultValue(true).HasColumnName("estado");
        });

        modelBuilder.Entity<HistorialLecturaUsoOt>(entity =>
        {
            entity.HasKey(e => e.IdHLecturaUsoOt).HasName("PK_HISTORIAL_LECTURA_USO_OT");

            entity.ToTable("historial_lectura_uso_ot", EsquemaOperativo);

            entity.Property(e => e.IdHLecturaUsoOt).ValueGeneratedOnAdd().HasColumnName("id_h_lectura_uso_ot");
            entity.Property(e => e.IdOrden).HasColumnName("id_orden");
            entity.Property(e => e.LecturaAnterior).HasColumnName("lectura_anterior");
            entity.Property(e => e.LecturaNueva).HasColumnName("lectura_nueva");
            entity.Property(e => e.IdUsuario).HasColumnName("id_usuario");
            entity.Property(e => e.FechaHora).HasColumnName("fecha_hora");

            entity.HasOne(d => d.IdOrdenNavigation).WithMany(p => p.HistorialLecturaUsoOts)
                .HasForeignKey(d => d.IdOrden)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HISTORIAL_LECTURA_USO_OT_ORDEN");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany()
                .HasForeignKey(d => d.IdUsuario)
                .HasConstraintName("FK_HISTORIAL_LECTURA_USO_OT_USUARIO");
        });

        modelBuilder.Entity<ReglaAviso>(entity =>
        {
            entity.HasKey(e => e.Codigo).HasName("PK_REGLA_AVISO");

            entity.ToTable("regla_aviso", EsquemaOperativo);

            entity.Property(e => e.Codigo).HasMaxLength(30).HasColumnName("codigo");
            entity.Property(e => e.Nombre).HasMaxLength(120).HasColumnName("nombre");
            entity.Property(e => e.Activa).HasColumnName("activa");
            entity.Property(e => e.Umbral).HasColumnName("umbral");
            entity.Property(e => e.UmbralCritico).HasColumnName("umbral_critico");
            entity.Property(e => e.VentanaDias).HasColumnName("ventana_dias");
            entity.Property(e => e.VentanaCriticaDias).HasColumnName("ventana_critica_dias");
            entity.Property(e => e.MinimoDatos).HasColumnName("minimo_datos");
        });

        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasKey(e => e.IdCliente).HasName("PK_CLIENTE");

            entity.ToTable("cliente", EsquemaOperativo);

            entity.HasIndex(e => e.Correo, "UK_CORREO_CLIENTE").IsUnique();

            entity.HasIndex(e => e.Nombre, "UK_NOMBRE_CLIENTE").IsUnique();

            entity.HasIndex(e => e.RazonSocial, "UK_RAZON_SOCIAL_CLIENTE").IsUnique();

            entity.HasIndex(e => e.Telefono, "UK_TELEFONO_CLIENTE").IsUnique();

            entity.Property(e => e.IdCliente)
                .HasDefaultValueSql("nextval('" + EsquemaOperativo + ".\"SEC_CLIENTE_ID\"'::regclass)")
                .HasColumnName("id_cliente");
            entity.Property(e => e.Correo)
                .HasMaxLength(254)
                .HasColumnName("correo");
            entity.Property(e => e.Direccion)
                .HasMaxLength(255)
                .HasColumnName("direccion");
            entity.Property(e => e.Estado).HasColumnName("estado");
            entity.Property(e => e.FechaIngreso)
                .HasDefaultValueSql("CURRENT_DATE")
                .HasColumnName("fecha_ingreso");
            entity.Property(e => e.Nombre)
                .HasMaxLength(120)
                .HasColumnName("nombre");
            entity.Property(e => e.Observaciones).HasColumnName("observaciones");
            entity.Property(e => e.RazonSocial)
                .HasMaxLength(160)
                .HasColumnName("razon_social");
            entity.Property(e => e.Telefono)
                .HasMaxLength(30)
                .HasColumnName("telefono");
        });

        modelBuilder.Entity<EstadoOt>(entity =>
        {
            entity.HasKey(e => e.IdEstadoOt).HasName("PK_ESTADO_OT");

            entity.ToTable("estado_ot", EsquemaOperativo);

            entity.HasIndex(e => e.Nombre, "UK_NOMBRE_ESTADO_OT").IsUnique();

            entity.Property(e => e.IdEstadoOt)
                .HasDefaultValueSql("nextval('" + EsquemaOperativo + ".\"SEC_ESTADO_OT_ID\"'::regclass)")
                .HasColumnName("id_estado_ot");
            entity.Property(e => e.Estado).HasColumnName("estado");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.Nombre)
                .HasMaxLength(60)
                .HasColumnName("nombre");
            entity.Property(e => e.Orden).HasColumnName("orden");
            entity.Property(e => e.Fase)
                .HasMaxLength(20)
                .HasColumnName("fase");
            entity.Property(e => e.ExigeTecnico).HasColumnName("exige_tecnico");
            entity.Property(e => e.CompletaFecha)
                .HasMaxLength(10)
                .HasColumnName("completa_fecha");
            entity.Property(e => e.Tono)
                .HasMaxLength(12)
                .HasColumnName("tono");
            entity.Property(e => e.CodigoAlerta)
                .HasMaxLength(40)
                .HasColumnName("codigo_alerta");
            entity.Property(e => e.EsInicial).HasColumnName("es_inicial");
        });

        modelBuilder.Entity<TransicionEstadoOt>(entity =>
        {
            entity.HasKey(e => new { e.IdEstadoOrigen, e.IdEstadoDestino }).HasName("PK_TRANSICION_ESTADO_OT");

            entity.ToTable("transicion_estado_ot", EsquemaOperativo);

            entity.Property(e => e.IdEstadoOrigen).HasColumnName("id_estado_origen");
            entity.Property(e => e.IdEstadoDestino).HasColumnName("id_estado_destino");
            entity.Property(e => e.ExigeMotivo).HasColumnName("exige_motivo");
            entity.Property(e => e.PuedeTecnico).HasColumnName("puede_tecnico");
            entity.Property(e => e.Accion)
                .HasMaxLength(40)
                .HasColumnName("accion");
            entity.Property(e => e.Principal).HasColumnName("principal");
            entity.Property(e => e.AlAsignarTecnico).HasColumnName("al_asignar_tecnico");

            entity.HasOne(d => d.IdEstadoOrigenNavigation).WithMany(p => p.TransicionesDeOrigen)
                .HasForeignKey(d => d.IdEstadoOrigen)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TRANSICION_ESTADO_OT_ORIGEN");

            entity.HasOne(d => d.IdEstadoDestinoNavigation).WithMany(p => p.TransicionesDeDestino)
                .HasForeignKey(d => d.IdEstadoDestino)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TRANSICION_ESTADO_OT_DESTINO");
        });

        modelBuilder.Entity<HistorialEstadoOt>(entity =>
        {
            entity.HasKey(e => e.IdHEstadoOt).HasName("PK_HISTORIAL_ESTADO_OT");

            entity.ToTable("historial_estado_ot", EsquemaOperativo);

            entity.Property(e => e.IdHEstadoOt)
                .HasDefaultValueSql("nextval('" + EsquemaOperativo + ".\"SEC_HISTORIAL_ESTADO_OT_ID\"'::regclass)")
                .HasColumnName("id_h_estado_ot");
            entity.Property(e => e.FechaHora).HasColumnName("fecha_hora");
            entity.Property(e => e.IdEstadoOt).HasColumnName("id_estado_ot");
            entity.Property(e => e.IdMotivoEs).HasColumnName("id_motivo_es");
            entity.Property(e => e.IdOrden).HasColumnName("id_orden");
            entity.Property(e => e.Observacion).HasColumnName("observacion");
            entity.Property(e => e.IdEstadoAnterior).HasColumnName("id_estado_anterior");
            entity.Property(e => e.IdUsuario).HasColumnName("id_usuario");

            entity.HasOne(d => d.IdEstadoAnteriorNavigation).WithMany()
                .HasForeignKey(d => d.IdEstadoAnterior)
                .HasConstraintName("FK_HISTORIAL_ESTADO_OT_ANTERIOR");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany()
                .HasForeignKey(d => d.IdUsuario)
                .HasConstraintName("FK_HISTORIAL_ESTADO_OT_USUARIO");

            entity.HasOne(d => d.IdEstadoOtNavigation).WithMany(p => p.HistorialEstadoOts)
                .HasForeignKey(d => d.IdEstadoOt)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ESTADO_OT_HISTORIAL_ESTADO_OT");

            entity.HasOne(d => d.IdMotivoEsNavigation).WithMany(p => p.HistorialEstadoOts)
                .HasForeignKey(d => d.IdMotivoEs)
                .HasConstraintName("FK_MOTIVO_ESTADO_OT_HISTORIAL_ESTADO_OT");

            entity.HasOne(d => d.IdOrdenNavigation).WithMany(p => p.HistorialEstadoOts)
                .HasForeignKey(d => d.IdOrden)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ORDEN_TRABAJO_HISTORIAL_ESTADO_OT");
        });

        modelBuilder.Entity<HistorialRepuestoOt>(entity =>
        {
            entity.HasKey(e => e.IdHRepuestoOt).HasName("PK_HISTORIAL_REPUESTO_OT");

            entity.ToTable("historial_repuesto_ot", EsquemaOperativo);

            entity.Property(e => e.IdHRepuestoOt).ValueGeneratedOnAdd().HasColumnName("id_h_repuesto_ot");
            entity.Property(e => e.IdOrden).HasColumnName("id_orden");
            entity.Property(e => e.IdRepuesto).HasColumnName("id_repuesto");
            entity.Property(e => e.Accion).HasMaxLength(12).HasColumnName("accion");
            entity.Property(e => e.CantidadAnterior).HasColumnName("cantidad_anterior");
            entity.Property(e => e.CantidadNueva).HasColumnName("cantidad_nueva");
            entity.Property(e => e.EstadoAnterior).HasMaxLength(12).HasColumnName("estado_anterior");
            entity.Property(e => e.EstadoNuevo).HasMaxLength(12).HasColumnName("estado_nuevo");
            entity.Property(e => e.IdUsuario).HasColumnName("id_usuario");
            entity.Property(e => e.FechaHora).HasColumnName("fecha_hora");
            entity.Property(e => e.IdMovimiento).HasColumnName("id_movimiento");
            entity.Property(e => e.Observacion).HasColumnName("observacion");

            entity.HasOne(d => d.IdOrdenNavigation).WithMany(p => p.HistorialRepuestoOts)
                .HasForeignKey(d => d.IdOrden)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HISTORIAL_REPUESTO_OT_ORDEN");

            entity.HasOne(d => d.IdRepuestoNavigation).WithMany()
                .HasForeignKey(d => d.IdRepuesto)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HISTORIAL_REPUESTO_OT_REPUESTO");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany()
                .HasForeignKey(d => d.IdUsuario)
                .HasConstraintName("FK_HISTORIAL_REPUESTO_OT_USUARIO");

            entity.HasOne(d => d.IdMovimientoNavigation).WithMany()
                .HasForeignKey(d => d.IdMovimiento)
                .HasConstraintName("FK_HISTORIAL_REPUESTO_OT_MOVIMIENTO");
        });

        modelBuilder.Entity<HistorialTecnicoOt>(entity =>
        {
            entity.HasKey(e => e.IdHTecnicoOt).HasName("PK_HISTORIAL_TECNICO_OT");

            entity.ToTable("historial_tecnico_ot", EsquemaOperativo);

            entity.Property(e => e.IdHTecnicoOt).ValueGeneratedOnAdd().HasColumnName("id_h_tecnico_ot");
            entity.Property(e => e.IdOrden).HasColumnName("id_orden");
            entity.Property(e => e.IdTecnicoAnterior).HasColumnName("id_tecnico_anterior");
            entity.Property(e => e.IdTecnicoNuevo).HasColumnName("id_tecnico_nuevo");
            entity.Property(e => e.IdUsuario).HasColumnName("id_usuario");
            entity.Property(e => e.FechaHora).HasColumnName("fecha_hora");
            entity.Property(e => e.Observacion).HasColumnName("observacion");

            entity.HasOne(d => d.IdOrdenNavigation).WithMany(p => p.HistorialTecnicoOts)
                .HasForeignKey(d => d.IdOrden)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HISTORIAL_TECNICO_OT_ORDEN");

            entity.HasOne(d => d.IdTecnicoAnteriorNavigation).WithMany()
                .HasForeignKey(d => d.IdTecnicoAnterior)
                .HasConstraintName("FK_HISTORIAL_TECNICO_OT_ANTERIOR");

            entity.HasOne(d => d.IdTecnicoNuevoNavigation).WithMany()
                .HasForeignKey(d => d.IdTecnicoNuevo)
                .HasConstraintName("FK_HISTORIAL_TECNICO_OT_NUEVO");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany()
                .HasForeignKey(d => d.IdUsuario)
                .HasConstraintName("FK_HISTORIAL_TECNICO_OT_USUARIO");
        });

        modelBuilder.Entity<HistorialRecomendacionIa>(entity =>
        {
            entity.HasKey(e => e.IdRecomendacionIa).HasName("PK_HISTORIAL_RECOMENDACIONES_IA");

            entity.ToTable("historial_recomendaciones_ia", EsquemaOperativo);

            entity.HasIndex(e => e.IdOrden, "IDX_HISTORIAL_RECOMENDACIONES_IA_ORDEN");

            entity.Property(e => e.IdRecomendacionIa)
                .HasDefaultValueSql("nextval('" + EsquemaOperativo + ".\"SEC_HISTORIAL_RECOMENDACIONES_IA_ID\"'::regclass)")
                .HasColumnName("id_recomendacion_ia");
            entity.Property(e => e.Estado)
                .HasDefaultValue(true)
                .HasColumnName("estado");
            entity.Property(e => e.IdOrden).HasColumnName("id_orden");
            entity.Property(e => e.Recomendacion).HasColumnName("recomendacion");
            entity.Property(e => e.Sesion).HasColumnName("sesion");
            entity.Property(e => e.Consulta).HasColumnName("consulta");
            entity.Property(e => e.NumeroIntento).HasColumnName("numero_intento");
            entity.Property(e => e.FechaGeneracion)
                .HasDefaultValueSql("now()")
                .HasColumnName("fecha_generacion");
            entity.Property(e => e.Resultado)
                .HasMaxLength(20)
                .HasDefaultValue("pendiente")
                .HasColumnName("resultado");
            entity.Property(e => e.Modo)
                .HasMaxLength(20)
                .HasDefaultValue("ia")
                .HasColumnName("modo");

            entity.HasOne(d => d.IdOrdenNavigation).WithMany(p => p.HistorialRecomendacionesIa)
                .HasForeignKey(d => d.IdOrden)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ORDEN_TRABAJO_HISTORIAL_RECOMENDACIONES_IA");
        });

        modelBuilder.Entity<ModeloMaquina>(entity =>
        {
            entity.HasKey(e => e.IdModeloMaquina).HasName("PK_MODELO_MAQUINA");

            entity.ToTable("modelo_maquina", EsquemaOperativo);

            entity.HasIndex(e => e.IdTipoMaquina, "IX_MODELO_MAQUINA_ID_TIPO_MAQUINA");

            entity.Property(e => e.IdModeloMaquina)
                .HasDefaultValueSql("nextval('" + EsquemaOperativo + ".\"SEC_MODELO_MAQUINA_ID\"'::regclass)")
                .HasColumnName("id_modelo_maquina");
            entity.Property(e => e.Activo)
                .HasDefaultValue(true)
                .HasColumnName("activo");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.IdTipoMaquina).HasColumnName("id_tipo_maquina");
            entity.Property(e => e.Nombre)
                .HasMaxLength(60)
                .HasColumnName("nombre");

            entity.HasOne(d => d.IdTipoMaquinaNavigation).WithMany(p => p.ModeloMaquinas)
                .HasForeignKey(d => d.IdTipoMaquina)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TIPO_MAQUINA_MODELO_MAQUINA");
        });

        modelBuilder.Entity<MotivoEstadoOt>(entity =>
        {
            entity.HasKey(e => e.IdMotivoEs).HasName("PK_MOTIVO_ESTADO_OT");

            entity.ToTable("motivo_estado_ot", EsquemaOperativo);

            entity.Property(e => e.IdMotivoEs)
                .HasDefaultValueSql("nextval('" + EsquemaOperativo + ".\"SEC_MOTIVO_ESTADO_OT_ID\"'::regclass)")
                .HasColumnName("id_motivo_es");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.Codigo).HasMaxLength(30).HasColumnName("codigo");
        });

        modelBuilder.Entity<EvidenciaOt>(entity =>
        {
            entity.HasKey(e => e.IdEvidencia).HasName("PK_EVIDENCIA_OT");

            entity.ToTable("evidencia_ot", EsquemaOperativo);

            entity.Property(e => e.IdEvidencia)
                .HasDefaultValueSql("nextval('" + EsquemaOperativo + ".\"SEC_EVIDENCIA_OT_ID\"'::regclass)")
                .HasColumnName("id_evidencia");
            entity.Property(e => e.IdOrden).HasColumnName("id_orden");
            entity.Property(e => e.IdUsuario).HasColumnName("id_usuario");
            entity.Property(e => e.Tipo)
                .HasMaxLength(20)
                .HasColumnName("tipo");
            entity.Property(e => e.Observacion).HasColumnName("observacion");
            entity.Property(e => e.FechaRegistro).HasColumnName("fecha_registro");
            entity.Property(e => e.CreadoEn)
                .HasDefaultValueSql("now()")
                .HasColumnName("creado_en");
            entity.Property(e => e.NombreArchivo)
                .HasMaxLength(255)
                .HasColumnName("nombre_archivo");
            entity.Property(e => e.TipoContenido)
                .HasMaxLength(100)
                .HasColumnName("tipo_contenido");
            entity.Property(e => e.Archivo).HasColumnName("archivo");
        });

        modelBuilder.Entity<TerminosCondiciones>(entity =>
        {
            entity.HasKey(e => e.IdTerminos);

            entity.ToTable("terminos_condiciones", EsquemaOperativo,
                tabla => tabla.HasCheckConstraint("CK_TERMINOS_SIEMPRE_VIGENTE", "vigente"));

            entity.Property(e => e.IdTerminos).ValueGeneratedOnAdd().HasColumnName("id_terminos");
            entity.Property(e => e.Version).HasColumnName("version");
            entity.Property(e => e.Titulo).HasMaxLength(200).HasColumnName("titulo");
            entity.Property(e => e.Texto).HasColumnName("texto");
            entity.Property(e => e.DatosAlmacenados).HasColumnName("datos_almacenados");
            entity.Property(e => e.Vigente).HasColumnName("vigente");
            entity.Property(e => e.CreadoEn).HasDefaultValueSql("now()").HasColumnName("creado_en");
            entity.Property(e => e.IdUsuarioAutor).HasColumnName("id_usuario_autor");
        });

        modelBuilder.Entity<AceptacionTerminos>(entity =>
        {
            entity.HasKey(e => e.IdAceptacion);

            entity.ToTable("aceptacion_terminos", EsquemaOperativo);

            entity.Property(e => e.IdAceptacion).ValueGeneratedOnAdd().HasColumnName("id_aceptacion");
            entity.Property(e => e.IdUsuario).HasColumnName("id_usuario");
            entity.Property(e => e.IdTerminos).HasColumnName("id_terminos");
            entity.Property(e => e.AceptadoEn).HasDefaultValueSql("now()").HasColumnName("aceptado_en");
        });

        modelBuilder.Entity<MovimientoStock>(entity =>
        {
            entity.HasKey(e => e.IdMovimiento).HasName("PK_MOVIMIENTO_STOCK");

            entity.ToTable("movimiento_stock", EsquemaOperativo);

            entity.HasIndex(e => e.IdUsuario, "IX_MOVIMIENTO_STOCK_ID_USUARIO");

            entity.Property(e => e.IdMovimiento)
                .HasDefaultValueSql("nextval('" + EsquemaOperativo + ".\"SEC_MOVIMIENTO_STOCK_ID\"'::regclass)")
                .HasColumnName("id_movimiento");
            entity.Property(e => e.Cantidad).HasColumnName("cantidad");
            entity.Property(e => e.Fecha).HasColumnName("fecha");
            entity.Property(e => e.IdRepuesto).HasColumnName("id_repuesto");
            entity.Property(e => e.IdUsuario).HasColumnName("id_usuario");
            entity.Property(e => e.Tipo)
                .HasMaxLength(20)
                .HasColumnName("tipo");
            entity.Property(e => e.Motivo)
                .HasMaxLength(200)
                .HasColumnName("motivo");
            entity.Property(e => e.Ubicacion)
                .HasMaxLength(150)
                .HasColumnName("ubicacion");
            entity.Property(e => e.Observaciones).HasColumnName("observaciones");
            entity.Property(e => e.StockAnterior).HasColumnName("stock_anterior");
            entity.Property(e => e.StockPosterior).HasColumnName("stock_posterior");

            entity.HasOne(d => d.IdRepuestoNavigation).WithMany(p => p.MovimientoStocks)
                .HasForeignKey(d => d.IdRepuesto)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_REPUESTO_MOVIMIENTO_STOCK");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.MovimientoStocks)
                .HasForeignKey(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_USUARIO_MOVIMIENTO_STOCK");
        });

        modelBuilder.Entity<OrdenTrabajo>(entity =>
        {
            entity.HasKey(e => e.IdOrden).HasName("PK_ORDEN_TRABAJO");

            entity.ToTable("orden_trabajo", EsquemaOperativo);

            entity.HasIndex(e => e.IdUsuarioResponsable, "IX_ORDEN_TRABAJO_ID_USUARIO_RESPONSABLE");

            entity.HasIndex(e => e.IdUsuarioTecnico, "IX_ORDEN_TRABAJO_ID_USUARIO_TECNICO");

            entity.Property(e => e.IdOrden)
                .HasDefaultValueSql("nextval('" + EsquemaOperativo + ".\"SEC_ORDEN_TRABAJO_ID\"'::regclass)")
                .HasColumnName("id_orden");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.FechaCierre).HasColumnName("fecha_cierre");
            entity.Property(e => e.FechaCreada).HasColumnName("fecha_creada");
            entity.Property(e => e.FechaInicio).HasColumnName("fecha_inicio");
            entity.Property(e => e.IdActivo).HasColumnName("id_activo");
            entity.Property(e => e.IdCliente).HasColumnName("id_cliente");
            entity.Property(e => e.IdUsuarioResponsable).HasColumnName("id_usuario_responsable");
            entity.Property(e => e.IdUsuarioTecnico).HasColumnName("id_usuario_tecnico");
            entity.Property(e => e.Prioridad)
                .HasDefaultValue((short)3)
                .HasColumnName("prioridad");
            entity.Property(e => e.Resultado).HasColumnName("resultado");
            entity.Property(e => e.Tipo).HasColumnName("tipo");
            entity.Property(e => e.FechaPrevistaEjecucion).HasColumnName("fecha_prevista_ejecucion");
            entity.Property(e => e.TiempoEstimadoHoras).HasColumnName("tiempo_estimado_horas");
            entity.Property(e => e.IdCausaFalla).HasColumnName("id_causa_falla");
            entity.Property(e => e.LecturaUso).HasColumnName("lectura_uso");

            entity.HasOne(d => d.IdActivoNavigation).WithMany(p => p.OrdenTrabajos)
                .HasForeignKey(d => d.IdActivo)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ACTIVO_ORDEN_TRABAJO");

            entity.HasOne(d => d.IdClienteNavigation).WithMany(p => p.OrdenTrabajos)
                .HasForeignKey(d => d.IdCliente)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CLIENTE_ORDEN_TRABAJO");

            entity.HasOne(d => d.IdUsuarioResponsableNavigation).WithMany(p => p.OrdenTrabajoIdUsuarioResponsableNavigations)
                .HasForeignKey(d => d.IdUsuarioResponsable)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_USUARIO_RESPONSABLE_ORDEN_TRABAJO");

            entity.HasOne(d => d.IdUsuarioTecnicoNavigation).WithMany(p => p.OrdenTrabajoIdUsuarioTecnicoNavigations)
                .HasForeignKey(d => d.IdUsuarioTecnico)
                .HasConstraintName("FK_USUARIO_TECNICO_ORDEN_TRABAJO");

            entity.HasOne(d => d.IdCausaFallaNavigation).WithMany(p => p.OrdenTrabajos)
                .HasForeignKey(d => d.IdCausaFalla)
                .HasConstraintName("FK_ORDEN_TRABAJO_CAUSA_FALLA");
        });

        modelBuilder.Entity<OtRepuesto>(entity =>
        {
            entity.HasKey(e => e.IdOtRepuesto).HasName("PK_OT_REPUESTO");

            entity.ToTable("ot_repuesto", EsquemaOperativo);

            entity.Property(e => e.IdOtRepuesto)
                .HasDefaultValueSql("nextval('" + EsquemaOperativo + ".\"SEC_OT_REPUESTO_ID\"'::regclass)")
                .HasColumnName("id_ot_repuesto");
            entity.Property(e => e.IdOrden).HasColumnName("id_orden");
            entity.Property(e => e.IdRepuesto).HasColumnName("id_repuesto");
            entity.Property(e => e.Cantidad).HasColumnName("cantidad").HasDefaultValue(1);
            entity.Property(e => e.Estado).HasMaxLength(12).HasColumnName("estado").HasDefaultValue("reservado");
            entity.Property(e => e.IdMovimiento).HasColumnName("id_movimiento");

            entity.HasOne(d => d.IdMovimientoNavigation).WithMany()
                .HasForeignKey(d => d.IdMovimiento)
                .HasConstraintName("FK_OT_REPUESTO_MOVIMIENTO");

            entity.HasOne(d => d.IdOrdenNavigation).WithMany(p => p.OtRepuestos)
                .HasForeignKey(d => d.IdOrden)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ORDEN_TRABAJO_OT_REPUESTO");

            entity.HasOne(d => d.IdRepuestoNavigation).WithMany(p => p.OtRepuestos)
                .HasForeignKey(d => d.IdRepuesto)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_REPUESTO_OT_REPUESTO");
        });

        modelBuilder.Entity<Permiso>(entity =>
        {
            entity.HasKey(e => e.IdPermiso).HasName("PK_PERMISO");

            entity.ToTable("permiso", EsquemaOperativo);

            entity.HasIndex(e => e.Nombre, "UK_NOMBRE_PERMISO").IsUnique();

            entity.Property(e => e.IdPermiso)
                .HasDefaultValueSql("nextval('" + EsquemaOperativo + ".\"SEC_PERMISO_ID\"'::regclass)")
                .HasColumnName("id_permiso");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .HasColumnName("nombre");
        });

        modelBuilder.Entity<Repuesto>(entity =>
        {
            entity.HasKey(e => e.IdRepuesto).HasName("PK_REPUESTO");

            entity.ToTable("repuesto", EsquemaOperativo);

            entity.HasIndex(e => e.Codigo, "UK_CODIGO_REPUESTO").IsUnique();

            entity.Property(e => e.IdRepuesto)
                .HasDefaultValueSql("nextval('" + EsquemaOperativo + ".\"SEC_REPUESTO_ID\"'::regclass)")
                .HasColumnName("id_repuesto");
            entity.Property(e => e.CantMin).HasColumnName("cant_min");
            entity.Property(e => e.CostoUnitario).HasColumnName("costo_unitario");
            entity.Property(e => e.Cantidad).HasColumnName("cantidad");
            entity.Property(e => e.Codigo)
                .HasMaxLength(50)
                .HasColumnName("codigo");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.Estado).HasColumnName("estado");
            entity.Property(e => e.Marca)
                .HasMaxLength(100)
                .HasColumnName("marca");
            entity.Property(e => e.Nombre)
                .HasMaxLength(120)
                .HasColumnName("nombre");
            entity.Property(e => e.Ubicacion)
                .HasMaxLength(120)
                .HasColumnName("ubicacion");
        });

        modelBuilder.Entity<Rol>(entity =>
        {
            entity.HasKey(e => e.IdRol).HasName("PK_ROL");

            entity.ToTable("rol", EsquemaOperativo);

            entity.HasIndex(e => e.Nombre, "UK_NOMBRE_ROL").IsUnique();

            entity.Property(e => e.IdRol)
                .HasDefaultValueSql("nextval('" + EsquemaOperativo + ".\"SEC_ROL_ID\"'::regclass)")
                .HasColumnName("id_rol");
            entity.Property(e => e.Activo)
                .HasDefaultValue(true)
                .HasColumnName("activo");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.Nombre)
                .HasMaxLength(60)
                .HasColumnName("nombre");

            entity.HasMany(d => d.IdPermisos).WithMany(p => p.IdRols)
                .UsingEntity<Dictionary<string, object>>(
                    "RolPermiso",
                    r => r.HasOne<Permiso>().WithMany()
                        .HasForeignKey("IdPermiso")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_PERMISO_ROL_PERMISO"),
                    l => l.HasOne<Rol>().WithMany()
                        .HasForeignKey("IdRol")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_ROL_ROL_PERMISO"),
                    j =>
                    {
                        j.HasKey("IdRol", "IdPermiso").HasName("PK_ROL_PERMISO");
                        j.ToTable("rol_permiso", EsquemaOperativo);
                        j.HasIndex(new[] { "IdPermiso" }, "IX_ROL_PERMISO_ID_PERMISO");
                        j.IndexerProperty<long>("IdRol").HasColumnName("id_rol");
                        j.IndexerProperty<long>("IdPermiso").HasColumnName("id_permiso");
                    });
        });

        modelBuilder.Entity<TipoMaquina>(entity =>
        {
            entity.HasKey(e => e.IdTipoMaquina).HasName("PK_TIPO_MAQUINA");

            entity.ToTable("tipo_maquina", EsquemaOperativo);

            entity.HasIndex(e => e.Nombre, "UK_NOMBRE_TIPO_MAQUINA").IsUnique();

            entity.Property(e => e.IdTipoMaquina)
                .HasDefaultValueSql("nextval('" + EsquemaOperativo + ".\"SEC_TIPO_MAQUINA_ID\"'::regclass)")
                .HasColumnName("id_tipo_maquina");
            entity.Property(e => e.Estado).HasColumnName("estado");
            entity.Property(e => e.Nombre)
                .HasMaxLength(60)
                .HasColumnName("nombre");
            entity.Property(e => e.CostoHoraParada).HasColumnName("costo_hora_parada");
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(e => e.IdUsuario).HasName("PK_USUARIO");

            entity.ToTable("usuario", EsquemaOperativo);

            entity.HasIndex(e => e.Mail, "UK_MAIL_USUARIO").IsUnique();

            entity.HasIndex(e => e.Telefono, "UK_TELEFONO_USUARIO").IsUnique();

            entity.HasIndex(e => e.IdKeycloak, "UK_ID_KEYCLOAK_USUARIO").IsUnique();

            entity.Property(e => e.IdUsuario)
                .HasDefaultValueSql("nextval('" + EsquemaOperativo + ".\"SEC_USUARIO_ID\"'::regclass)")
                .HasColumnName("id_usuario");
            entity.Property(e => e.Apellido)
                .HasMaxLength(100)
                .HasColumnName("apellido");
            entity.Property(e => e.Estado).HasColumnName("estado");
            entity.Property(e => e.FechaIngreso)
                .HasDefaultValueSql("CURRENT_DATE")
                .HasColumnName("fecha_ingreso");
            entity.Property(e => e.Mail)
                .HasMaxLength(254)
                .HasColumnName("mail");
            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .HasColumnName("nombre");
            entity.Property(e => e.Telefono)
                .HasMaxLength(30)
                .HasColumnName("telefono");
            entity.Property(e => e.IdKeycloak).HasColumnName("id_keycloak");

            entity.HasMany(d => d.IdRols).WithMany(p => p.IdUsuarios)
                .UsingEntity<Dictionary<string, object>>(
                    "UsuarioRol",
                    r => r.HasOne<Rol>().WithMany()
                        .HasForeignKey("IdRol")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_ROL_USUARIO_ROL"),
                    l => l.HasOne<Usuario>().WithMany()
                        .HasForeignKey("IdUsuario")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_USUARIO_USUARIO_ROL"),
                    j =>
                    {
                        j.HasKey("IdUsuario", "IdRol").HasName("PK_USUARIO_ROL");
                        j.ToTable("usuario_rol", EsquemaOperativo);
                        j.HasIndex(new[] { "IdRol" }, "IX_USUARIO_ROL_ID_ROL");
                        j.IndexerProperty<long>("IdUsuario").HasColumnName("id_usuario");
                        j.IndexerProperty<long>("IdRol").HasColumnName("id_rol");
                    });
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
