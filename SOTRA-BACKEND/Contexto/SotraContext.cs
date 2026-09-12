using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Contexto;

public partial class SotraContext : DbContext
{
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

    public virtual DbSet<HistorialEstadoOt> HistorialEstadoOts { get; set; }

    public virtual DbSet<ModeloMaquina> ModeloMaquinas { get; set; }

    public virtual DbSet<MotivoEstadoOt> MotivoEstadoOts { get; set; }

    public virtual DbSet<MovimientoStock> MovimientoStocks { get; set; }

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
            DotNetEnv.Env.TraversePath().Load();

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

            entity.ToTable("activo", "sotra-production");

            entity.HasIndex(e => e.IdModeloMaquina, "IX_ACTIVO_ID_MODELO_MAQUINA");

            entity.HasIndex(e => e.NumSerie, "UK_NUM_SERIE_ACTIVO").IsUnique();

            entity.Property(e => e.IdActivo)
                .HasDefaultValueSql("nextval('\"sotra-production\".\"SEC_ACTIVO_ID\"'::regclass)")
                .HasColumnName("id_activo");
            entity.Property(e => e.Activo1)
                .HasDefaultValue(true)
                .HasColumnName("activo");
            entity.Property(e => e.AnioFabricacion).HasColumnName("anio_fabricacion");
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

        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasKey(e => e.IdCliente).HasName("PK_CLIENTE");

            entity.ToTable("cliente", "sotra-production");

            entity.HasIndex(e => e.Correo, "UK_CORREO_CLIENTE").IsUnique();

            entity.HasIndex(e => e.Nombre, "UK_NOMBRE_CLIENTE").IsUnique();

            entity.HasIndex(e => e.RazonSocial, "UK_RAZON_SOCIAL_CLIENTE").IsUnique();

            entity.HasIndex(e => e.Telefono, "UK_TELEFONO_CLIENTE").IsUnique();

            entity.Property(e => e.IdCliente)
                .HasDefaultValueSql("nextval('\"sotra-production\".\"SEC_CLIENTE_ID\"'::regclass)")
                .HasColumnName("id_cliente");
            entity.Property(e => e.Correo)
                .HasMaxLength(254)
                .HasColumnName("correo");
            entity.Property(e => e.Direccion)
                .HasMaxLength(255)
                .HasColumnName("direccion");
            entity.Property(e => e.Estado).HasColumnName("estado");
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

            entity.ToTable("estado_ot", "sotra-production");

            entity.HasIndex(e => e.Nombre, "UK_NOMBRE_ESTADO_OT").IsUnique();

            entity.Property(e => e.IdEstadoOt)
                .HasDefaultValueSql("nextval('\"sotra-production\".\"SEC_ESTADO_OT_ID\"'::regclass)")
                .HasColumnName("id_estado_ot");
            entity.Property(e => e.Activo).HasColumnName("activo");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.Nombre)
                .HasMaxLength(60)
                .HasColumnName("nombre");
        });

        modelBuilder.Entity<HistorialEstadoOt>(entity =>
        {
            entity.HasKey(e => e.IdHEstadoOt).HasName("PK_HISTORIAL_ESTADO_OT");

            entity.ToTable("historial_estado_ot", "sotra-production");

            entity.Property(e => e.IdHEstadoOt)
                .HasDefaultValueSql("nextval('\"sotra-production\".\"SEC_HISTORIAL_ESTADO_OT_ID\"'::regclass)")
                .HasColumnName("id_h_estado_ot");
            entity.Property(e => e.FechaHora).HasColumnName("fecha_hora");
            entity.Property(e => e.IdEstadoOt).HasColumnName("id_estado_ot");
            entity.Property(e => e.IdMotivoEs).HasColumnName("id_motivo_es");
            entity.Property(e => e.IdOrden).HasColumnName("id_orden");
            entity.Property(e => e.Observacion).HasColumnName("observacion");

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

        modelBuilder.Entity<ModeloMaquina>(entity =>
        {
            entity.HasKey(e => e.IdModeloMaquina).HasName("PK_MODELO_MAQUINA");

            entity.ToTable("modelo_maquina", "sotra-production");

            entity.HasIndex(e => e.IdTipoMaquina, "IX_MODELO_MAQUINA_ID_TIPO_MAQUINA");

            entity.Property(e => e.IdModeloMaquina)
                .HasDefaultValueSql("nextval('\"sotra-production\".\"SEC_MODELO_MAQUINA_ID\"'::regclass)")
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

            entity.ToTable("motivo_estado_ot", "sotra-production");

            entity.Property(e => e.IdMotivoEs)
                .HasDefaultValueSql("nextval('\"sotra-production\".\"SEC_MOTIVO_ESTADO_OT_ID\"'::regclass)")
                .HasColumnName("id_motivo_es");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
        });

        modelBuilder.Entity<MovimientoStock>(entity =>
        {
            entity.HasKey(e => e.IdMovimiento).HasName("PK_MOVIMIENTO_STOCK");

            entity.ToTable("movimiento_stock", "sotra-production");

            entity.HasIndex(e => e.IdUsuario, "IX_MOVIMIENTO_STOCK_ID_USUARIO");

            entity.Property(e => e.IdMovimiento)
                .HasDefaultValueSql("nextval('\"sotra-production\".\"SEC_MOVIMIENTO_STOCK_ID\"'::regclass)")
                .HasColumnName("id_movimiento");
            entity.Property(e => e.Cantidad)
                .HasPrecision(4, 2)
                .HasColumnName("cantidad");
            entity.Property(e => e.Estado).HasColumnName("estado");
            entity.Property(e => e.Fecha).HasColumnName("fecha");
            entity.Property(e => e.IdRepuesto).HasColumnName("id_repuesto");
            entity.Property(e => e.IdUsuario).HasColumnName("id_usuario");

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

            entity.ToTable("orden_trabajo", "sotra-production");

            entity.HasIndex(e => e.IdUsuarioResponsable, "IX_ORDEN_TRABAJO_ID_USUARIO_RESPONSABLE");

            entity.HasIndex(e => e.IdUsuarioTecnico, "IX_ORDEN_TRABAJO_ID_USUARIO_TECNICO");

            entity.Property(e => e.IdOrden)
                .HasDefaultValueSql("nextval('\"sotra-production\".\"SEC_ORDEN_TRABAJO_ID\"'::regclass)")
                .HasColumnName("id_orden");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.FechaCierre).HasColumnName("fecha_cierre");
            entity.Property(e => e.FechaCreada).HasColumnName("fecha_creada");
            entity.Property(e => e.FechaInicio).HasColumnName("fecha_inicio");
            entity.Property(e => e.IdActivo).HasColumnName("id_activo");
            entity.Property(e => e.IdCliente).HasColumnName("id_cliente");
            entity.Property(e => e.IdUsuarioResponsable).HasColumnName("id_usuario_responsable");
            entity.Property(e => e.IdUsuarioTecnico).HasColumnName("id_usuario_tecnico");
            entity.Property(e => e.RecomendacionIa).HasColumnName("recomendacion_ia");
            entity.Property(e => e.Resultado).HasColumnName("resultado");
            entity.Property(e => e.Tipo).HasColumnName("tipo");

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
        });

        modelBuilder.Entity<OtRepuesto>(entity =>
        {
            entity.HasKey(e => e.IdOtRepuesto).HasName("PK_OT_REPUESTO");

            entity.ToTable("ot_repuesto", "sotra-production");

            entity.Property(e => e.IdOtRepuesto)
                .HasDefaultValueSql("nextval('\"sotra-production\".\"SEC_OT_REPUESTO_ID\"'::regclass)")
                .HasColumnName("id_ot_repuesto");
            entity.Property(e => e.IdOrden).HasColumnName("id_orden");
            entity.Property(e => e.IdRepuesto).HasColumnName("id_repuesto");

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

            entity.ToTable("permiso", "sotra-production");

            entity.HasIndex(e => e.Nombre, "UK_NOMBRE_PERMISO").IsUnique();

            entity.Property(e => e.IdPermiso)
                .HasDefaultValueSql("nextval('\"sotra-production\".\"SEC_PERMISO_ID\"'::regclass)")
                .HasColumnName("id_permiso");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .HasColumnName("nombre");
        });

        modelBuilder.Entity<Repuesto>(entity =>
        {
            entity.HasKey(e => e.IdRepuesto).HasName("PK_REPUESTO");

            entity.ToTable("repuesto", "sotra-production");

            entity.HasIndex(e => e.Codigo, "UK_CODIGO_REPUESTO").IsUnique();

            entity.Property(e => e.IdRepuesto)
                .HasDefaultValueSql("nextval('\"sotra-production\".\"SEC_REPUESTO_ID\"'::regclass)")
                .HasColumnName("id_repuesto");
            entity.Property(e => e.CantMin).HasColumnName("cant_min");
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

            entity.ToTable("rol", "sotra-production");

            entity.HasIndex(e => e.Nombre, "UK_NOMBRE_ROL").IsUnique();

            entity.Property(e => e.IdRol)
                .HasDefaultValueSql("nextval('\"sotra-production\".\"SEC_ROL_ID\"'::regclass)")
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
                        j.ToTable("rol_permiso", "sotra-production");
                        j.HasIndex(new[] { "IdPermiso" }, "IX_ROL_PERMISO_ID_PERMISO");
                        j.IndexerProperty<long>("IdRol").HasColumnName("id_rol");
                        j.IndexerProperty<long>("IdPermiso").HasColumnName("id_permiso");
                    });
        });

        modelBuilder.Entity<TipoMaquina>(entity =>
        {
            entity.HasKey(e => e.IdTipoMaquina).HasName("PK_TIPO_MAQUINA");

            entity.ToTable("tipo_maquina", "sotra-production");

            entity.HasIndex(e => e.Nombre, "UK_NOMBRE_TIPO_MAQUINA").IsUnique();

            entity.Property(e => e.IdTipoMaquina)
                .HasDefaultValueSql("nextval('\"sotra-production\".\"SEC_TIPO_MAQUINA_ID\"'::regclass)")
                .HasColumnName("id_tipo_maquina");
            entity.Property(e => e.Nombre)
                .HasMaxLength(60)
                .HasColumnName("nombre");
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(e => e.IdUsuario).HasName("PK_USUARIO");

            entity.ToTable("usuario", "sotra-production");

            entity.HasIndex(e => e.Mail, "UK_MAIL_USUARIO").IsUnique();

            entity.HasIndex(e => e.Telefono, "UK_TELEFONO_USUARIO").IsUnique();

            entity.Property(e => e.IdUsuario)
                .HasDefaultValueSql("nextval('\"sotra-production\".\"SEC_USUARIO_ID\"'::regclass)")
                .HasColumnName("id_usuario");
            entity.Property(e => e.Apellido)
                .HasMaxLength(100)
                .HasColumnName("apellido");
            entity.Property(e => e.Contrasena)
                .HasMaxLength(255)
                .HasColumnName("contrasena");
            entity.Property(e => e.Estado).HasColumnName("estado");
            entity.Property(e => e.FechaIngreso).HasColumnName("fecha_ingreso");
            entity.Property(e => e.Mail)
                .HasMaxLength(254)
                .HasColumnName("mail");
            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .HasColumnName("nombre");
            entity.Property(e => e.Telefono)
                .HasMaxLength(30)
                .HasColumnName("telefono");

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
                        j.ToTable("usuario_rol", "sotra-production");
                        j.HasIndex(new[] { "IdRol" }, "IX_USUARIO_ROL_ID_ROL");
                        j.IndexerProperty<long>("IdUsuario").HasColumnName("id_usuario");
                        j.IndexerProperty<long>("IdRol").HasColumnName("id_rol");
                    });
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
