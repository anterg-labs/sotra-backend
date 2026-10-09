--
-- PostgreSQL database dump
--


-- Dumped from database version 18.6 (Debian 18.6-1.pgdg13+2)
-- Dumped by pg_dump version 18.6 (Debian 18.6-1.pgdg13+2)

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: operativo; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA operativo;


--
-- Name: vectorial; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA vectorial;


--
-- Name: vector; Type: EXTENSION; Schema: -; Owner: -
--

CREATE EXTENSION IF NOT EXISTS vector WITH SCHEMA public;


--
-- Name: EXTENSION vector; Type: COMMENT; Schema: -; Owner: -
--

COMMENT ON EXTENSION vector IS 'vector data type and ivfflat and hnsw access methods';


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: activo; Type: TABLE; Schema: operativo; Owner: -
--

CREATE TABLE operativo.activo (
    id_activo bigint NOT NULL,
    id_cliente bigint NOT NULL,
    num_serie character varying(100) NOT NULL,
    descripcion text,
    id_modelo_maquina bigint NOT NULL,
    anio_fabricacion smallint,
    nombre character varying(100),
    ubicacion character varying(150),
    estado boolean DEFAULT true CONSTRAINT activo_activo_not_null NOT NULL,
    CONSTRAINT "CK_ACTIVO_ANIO_FABRICACION" CHECK (((anio_fabricacion IS NULL) OR ((anio_fabricacion >= 1900) AND (anio_fabricacion <= 2100))))
);


--
-- Name: SEC_ACTIVO_ID; Type: SEQUENCE; Schema: operativo; Owner: -
--

CREATE SEQUENCE operativo."SEC_ACTIVO_ID"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: SEC_ACTIVO_ID; Type: SEQUENCE OWNED BY; Schema: operativo; Owner: -
--

ALTER SEQUENCE operativo."SEC_ACTIVO_ID" OWNED BY operativo.activo.id_activo;


--
-- Name: cliente; Type: TABLE; Schema: operativo; Owner: -
--

CREATE TABLE operativo.cliente (
    id_cliente bigint NOT NULL,
    razon_social character varying(160) NOT NULL,
    nombre character varying(120) NOT NULL,
    correo character varying(254),
    telefono character varying(30),
    direccion character varying(255),
    observaciones text,
    estado boolean NOT NULL,
    fecha_ingreso date DEFAULT CURRENT_DATE NOT NULL
);


--
-- Name: SEC_CLIENTE_ID; Type: SEQUENCE; Schema: operativo; Owner: -
--

CREATE SEQUENCE operativo."SEC_CLIENTE_ID"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: SEC_CLIENTE_ID; Type: SEQUENCE OWNED BY; Schema: operativo; Owner: -
--

ALTER SEQUENCE operativo."SEC_CLIENTE_ID" OWNED BY operativo.cliente.id_cliente;


--
-- Name: estado_ot; Type: TABLE; Schema: operativo; Owner: -
--

CREATE TABLE operativo.estado_ot (
    id_estado_ot bigint NOT NULL,
    nombre character varying(60) NOT NULL,
    descripcion text,
    estado boolean CONSTRAINT estado_ot_activo_not_null NOT NULL
);


--
-- Name: SEC_ESTADO_OT_ID; Type: SEQUENCE; Schema: operativo; Owner: -
--

CREATE SEQUENCE operativo."SEC_ESTADO_OT_ID"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: SEC_ESTADO_OT_ID; Type: SEQUENCE OWNED BY; Schema: operativo; Owner: -
--

ALTER SEQUENCE operativo."SEC_ESTADO_OT_ID" OWNED BY operativo.estado_ot.id_estado_ot;


--
-- Name: historial_estado_ot; Type: TABLE; Schema: operativo; Owner: -
--

CREATE TABLE operativo.historial_estado_ot (
    id_h_estado_ot bigint NOT NULL,
    id_orden bigint NOT NULL,
    id_motivo_es bigint,
    id_estado_ot bigint NOT NULL,
    fecha_hora timestamp with time zone NOT NULL,
    observacion text
);


--
-- Name: SEC_HISTORIAL_ESTADO_OT_ID; Type: SEQUENCE; Schema: operativo; Owner: -
--

CREATE SEQUENCE operativo."SEC_HISTORIAL_ESTADO_OT_ID"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: SEC_HISTORIAL_ESTADO_OT_ID; Type: SEQUENCE OWNED BY; Schema: operativo; Owner: -
--

ALTER SEQUENCE operativo."SEC_HISTORIAL_ESTADO_OT_ID" OWNED BY operativo.historial_estado_ot.id_h_estado_ot;


--
-- Name: historial_recomendaciones_ia; Type: TABLE; Schema: operativo; Owner: -
--

CREATE TABLE operativo.historial_recomendaciones_ia (
    id_recomendacion_ia bigint NOT NULL,
    recomendacion text NOT NULL,
    id_orden bigint NOT NULL,
    estado boolean DEFAULT true NOT NULL
);


--
-- Name: SEC_HISTORIAL_RECOMENDACIONES_IA_ID; Type: SEQUENCE; Schema: operativo; Owner: -
--

CREATE SEQUENCE operativo."SEC_HISTORIAL_RECOMENDACIONES_IA_ID"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: SEC_HISTORIAL_RECOMENDACIONES_IA_ID; Type: SEQUENCE OWNED BY; Schema: operativo; Owner: -
--

ALTER SEQUENCE operativo."SEC_HISTORIAL_RECOMENDACIONES_IA_ID" OWNED BY operativo.historial_recomendaciones_ia.id_recomendacion_ia;


--
-- Name: modelo_maquina; Type: TABLE; Schema: operativo; Owner: -
--

CREATE TABLE operativo.modelo_maquina (
    id_modelo_maquina bigint NOT NULL,
    nombre character varying(60) NOT NULL,
    id_tipo_maquina bigint NOT NULL,
    descripcion text,
    activo boolean DEFAULT true NOT NULL
);


--
-- Name: SEC_MODELO_MAQUINA_ID; Type: SEQUENCE; Schema: operativo; Owner: -
--

CREATE SEQUENCE operativo."SEC_MODELO_MAQUINA_ID"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: SEC_MODELO_MAQUINA_ID; Type: SEQUENCE OWNED BY; Schema: operativo; Owner: -
--

ALTER SEQUENCE operativo."SEC_MODELO_MAQUINA_ID" OWNED BY operativo.modelo_maquina.id_modelo_maquina;


--
-- Name: motivo_estado_ot; Type: TABLE; Schema: operativo; Owner: -
--

CREATE TABLE operativo.motivo_estado_ot (
    id_motivo_es bigint NOT NULL,
    descripcion text
);


--
-- Name: SEC_MOTIVO_ESTADO_OT_ID; Type: SEQUENCE; Schema: operativo; Owner: -
--

CREATE SEQUENCE operativo."SEC_MOTIVO_ESTADO_OT_ID"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: SEC_MOTIVO_ESTADO_OT_ID; Type: SEQUENCE OWNED BY; Schema: operativo; Owner: -
--

ALTER SEQUENCE operativo."SEC_MOTIVO_ESTADO_OT_ID" OWNED BY operativo.motivo_estado_ot.id_motivo_es;


--
-- Name: movimiento_stock; Type: TABLE; Schema: operativo; Owner: -
--

CREATE TABLE operativo.movimiento_stock (
    id_movimiento bigint NOT NULL,
    estado boolean NOT NULL,
    fecha timestamp with time zone NOT NULL,
    cantidad numeric(4,2) NOT NULL,
    id_repuesto bigint NOT NULL,
    id_usuario bigint NOT NULL
);


--
-- Name: SEC_MOVIMIENTO_STOCK_ID; Type: SEQUENCE; Schema: operativo; Owner: -
--

CREATE SEQUENCE operativo."SEC_MOVIMIENTO_STOCK_ID"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: SEC_MOVIMIENTO_STOCK_ID; Type: SEQUENCE OWNED BY; Schema: operativo; Owner: -
--

ALTER SEQUENCE operativo."SEC_MOVIMIENTO_STOCK_ID" OWNED BY operativo.movimiento_stock.id_movimiento;


--
-- Name: orden_trabajo; Type: TABLE; Schema: operativo; Owner: -
--

CREATE TABLE operativo.orden_trabajo (
    id_orden bigint NOT NULL,
    id_activo bigint NOT NULL,
    id_cliente bigint NOT NULL,
    tipo boolean NOT NULL,
    descripcion text NOT NULL,
    fecha_creada timestamp with time zone NOT NULL,
    fecha_inicio timestamp with time zone,
    fecha_cierre timestamp with time zone,
    resultado text,
    id_usuario_responsable bigint NOT NULL,
    id_usuario_tecnico bigint,
    prioridad smallint DEFAULT 3 NOT NULL,
    id_recomendacion_ia bigint,
    CONSTRAINT "CK_ORDEN_TRABAJO_PRIORIDAD" CHECK (((prioridad >= 1) AND (prioridad <= 5)))
);


--
-- Name: SEC_ORDEN_TRABAJO_ID; Type: SEQUENCE; Schema: operativo; Owner: -
--

CREATE SEQUENCE operativo."SEC_ORDEN_TRABAJO_ID"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: SEC_ORDEN_TRABAJO_ID; Type: SEQUENCE OWNED BY; Schema: operativo; Owner: -
--

ALTER SEQUENCE operativo."SEC_ORDEN_TRABAJO_ID" OWNED BY operativo.orden_trabajo.id_orden;


--
-- Name: ot_repuesto; Type: TABLE; Schema: operativo; Owner: -
--

CREATE TABLE operativo.ot_repuesto (
    id_ot_repuesto bigint NOT NULL,
    id_orden bigint NOT NULL,
    id_repuesto bigint NOT NULL
);


--
-- Name: SEC_OT_REPUESTO_ID; Type: SEQUENCE; Schema: operativo; Owner: -
--

CREATE SEQUENCE operativo."SEC_OT_REPUESTO_ID"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: SEC_OT_REPUESTO_ID; Type: SEQUENCE OWNED BY; Schema: operativo; Owner: -
--

ALTER SEQUENCE operativo."SEC_OT_REPUESTO_ID" OWNED BY operativo.ot_repuesto.id_ot_repuesto;


--
-- Name: permiso; Type: TABLE; Schema: operativo; Owner: -
--

CREATE TABLE operativo.permiso (
    id_permiso bigint NOT NULL,
    nombre character varying(100) NOT NULL,
    descripcion text
);


--
-- Name: SEC_PERMISO_ID; Type: SEQUENCE; Schema: operativo; Owner: -
--

CREATE SEQUENCE operativo."SEC_PERMISO_ID"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: SEC_PERMISO_ID; Type: SEQUENCE OWNED BY; Schema: operativo; Owner: -
--

ALTER SEQUENCE operativo."SEC_PERMISO_ID" OWNED BY operativo.permiso.id_permiso;


--
-- Name: repuesto; Type: TABLE; Schema: operativo; Owner: -
--

CREATE TABLE operativo.repuesto (
    codigo character varying(50) NOT NULL,
    nombre character varying(120) NOT NULL,
    descripcion text,
    cantidad integer CONSTRAINT repuesto_unidad_not_null NOT NULL,
    estado boolean NOT NULL,
    ubicacion character varying(120),
    marca character varying(100),
    id_repuesto bigint NOT NULL,
    cant_min integer
);


--
-- Name: SEC_REPUESTO_ID; Type: SEQUENCE; Schema: operativo; Owner: -
--

CREATE SEQUENCE operativo."SEC_REPUESTO_ID"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: SEC_REPUESTO_ID; Type: SEQUENCE OWNED BY; Schema: operativo; Owner: -
--

ALTER SEQUENCE operativo."SEC_REPUESTO_ID" OWNED BY operativo.repuesto.id_repuesto;


--
-- Name: rol; Type: TABLE; Schema: operativo; Owner: -
--

CREATE TABLE operativo.rol (
    id_rol bigint NOT NULL,
    nombre character varying(60) NOT NULL,
    descripcion text,
    activo boolean DEFAULT true NOT NULL
);


--
-- Name: SEC_ROL_ID; Type: SEQUENCE; Schema: operativo; Owner: -
--

CREATE SEQUENCE operativo."SEC_ROL_ID"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: SEC_ROL_ID; Type: SEQUENCE OWNED BY; Schema: operativo; Owner: -
--

ALTER SEQUENCE operativo."SEC_ROL_ID" OWNED BY operativo.rol.id_rol;


--
-- Name: tipo_maquina; Type: TABLE; Schema: operativo; Owner: -
--

CREATE TABLE operativo.tipo_maquina (
    id_tipo_maquina bigint NOT NULL,
    nombre character varying(60) NOT NULL,
    estado boolean NOT NULL
);


--
-- Name: SEC_TIPO_MAQUINA_ID; Type: SEQUENCE; Schema: operativo; Owner: -
--

CREATE SEQUENCE operativo."SEC_TIPO_MAQUINA_ID"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: SEC_TIPO_MAQUINA_ID; Type: SEQUENCE OWNED BY; Schema: operativo; Owner: -
--

ALTER SEQUENCE operativo."SEC_TIPO_MAQUINA_ID" OWNED BY operativo.tipo_maquina.id_tipo_maquina;


--
-- Name: usuario; Type: TABLE; Schema: operativo; Owner: -
--

CREATE TABLE operativo.usuario (
    id_usuario bigint NOT NULL,
    nombre character varying(100) NOT NULL,
    apellido character varying(100) NOT NULL,
    mail character varying(254) NOT NULL,
    telefono character varying(30) NOT NULL,
    contrasena character varying(255) NOT NULL,
    estado boolean NOT NULL,
    fecha_ingreso date DEFAULT CURRENT_DATE NOT NULL
);


--
-- Name: SEC_USUARIO_ID; Type: SEQUENCE; Schema: operativo; Owner: -
--

CREATE SEQUENCE operativo."SEC_USUARIO_ID"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: SEC_USUARIO_ID; Type: SEQUENCE OWNED BY; Schema: operativo; Owner: -
--

ALTER SEQUENCE operativo."SEC_USUARIO_ID" OWNED BY operativo.usuario.id_usuario;


--
-- Name: rol_permiso; Type: TABLE; Schema: operativo; Owner: -
--

CREATE TABLE operativo.rol_permiso (
    id_rol bigint NOT NULL,
    id_permiso bigint NOT NULL
);


--
-- Name: usuario_rol; Type: TABLE; Schema: operativo; Owner: -
--

CREATE TABLE operativo.usuario_rol (
    id_usuario bigint NOT NULL,
    id_rol bigint NOT NULL
);


--
-- Name: documento_tecnico; Type: TABLE; Schema: vectorial; Owner: -
--

CREATE TABLE vectorial.documento_tecnico (
    id bigint NOT NULL,
    nombre_archivo character varying(255) NOT NULL,
    activo_id bigint,
    usa_sugerencias_ia boolean NOT NULL,
    cantidad_fragmentos integer DEFAULT 0 NOT NULL,
    archivo bytea NOT NULL,
    texto text DEFAULT ''::text NOT NULL,
    creado_en timestamp with time zone NOT NULL,
    documento_vectorial_id bigint
);


--
-- Name: documento_tecnico_id_seq; Type: SEQUENCE; Schema: vectorial; Owner: -
--

ALTER TABLE vectorial.documento_tecnico ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME vectorial.documento_tecnico_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: documentos; Type: TABLE; Schema: vectorial; Owner: -
--

CREATE TABLE vectorial.documentos (
    id bigint NOT NULL,
    nombre text NOT NULL,
    huella text NOT NULL,
    version_proceso text NOT NULL,
    procesado_en timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: documentos_id_seq; Type: SEQUENCE; Schema: vectorial; Owner: -
--

CREATE SEQUENCE vectorial.documentos_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: documentos_id_seq; Type: SEQUENCE OWNED BY; Schema: vectorial; Owner: -
--

ALTER SEQUENCE vectorial.documentos_id_seq OWNED BY vectorial.documentos.id;


--
-- Name: fragmentos; Type: TABLE; Schema: vectorial; Owner: -
--

CREATE TABLE vectorial.fragmentos (
    id bigint NOT NULL,
    documento_id bigint NOT NULL,
    posicion integer NOT NULL,
    pagina integer NOT NULL,
    texto text NOT NULL,
    embedding public.vector(1024) NOT NULL,
    texto_busqueda tsvector GENERATED ALWAYS AS (to_tsvector('spanish'::regconfig, texto)) STORED
);


--
-- Name: fragmentos_id_seq; Type: SEQUENCE; Schema: vectorial; Owner: -
--

CREATE SEQUENCE vectorial.fragmentos_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: fragmentos_id_seq; Type: SEQUENCE OWNED BY; Schema: vectorial; Owner: -
--

ALTER SEQUENCE vectorial.fragmentos_id_seq OWNED BY vectorial.fragmentos.id;


--
-- Name: activo id_activo; Type: DEFAULT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.activo ALTER COLUMN id_activo SET DEFAULT nextval('operativo."SEC_ACTIVO_ID"'::regclass);


--
-- Name: cliente id_cliente; Type: DEFAULT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.cliente ALTER COLUMN id_cliente SET DEFAULT nextval('operativo."SEC_CLIENTE_ID"'::regclass);


--
-- Name: estado_ot id_estado_ot; Type: DEFAULT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.estado_ot ALTER COLUMN id_estado_ot SET DEFAULT nextval('operativo."SEC_ESTADO_OT_ID"'::regclass);


--
-- Name: historial_estado_ot id_h_estado_ot; Type: DEFAULT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.historial_estado_ot ALTER COLUMN id_h_estado_ot SET DEFAULT nextval('operativo."SEC_HISTORIAL_ESTADO_OT_ID"'::regclass);


--
-- Name: historial_recomendaciones_ia id_recomendacion_ia; Type: DEFAULT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.historial_recomendaciones_ia ALTER COLUMN id_recomendacion_ia SET DEFAULT nextval('operativo."SEC_HISTORIAL_RECOMENDACIONES_IA_ID"'::regclass);


--
-- Name: modelo_maquina id_modelo_maquina; Type: DEFAULT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.modelo_maquina ALTER COLUMN id_modelo_maquina SET DEFAULT nextval('operativo."SEC_MODELO_MAQUINA_ID"'::regclass);


--
-- Name: motivo_estado_ot id_motivo_es; Type: DEFAULT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.motivo_estado_ot ALTER COLUMN id_motivo_es SET DEFAULT nextval('operativo."SEC_MOTIVO_ESTADO_OT_ID"'::regclass);


--
-- Name: movimiento_stock id_movimiento; Type: DEFAULT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.movimiento_stock ALTER COLUMN id_movimiento SET DEFAULT nextval('operativo."SEC_MOVIMIENTO_STOCK_ID"'::regclass);


--
-- Name: orden_trabajo id_orden; Type: DEFAULT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.orden_trabajo ALTER COLUMN id_orden SET DEFAULT nextval('operativo."SEC_ORDEN_TRABAJO_ID"'::regclass);


--
-- Name: ot_repuesto id_ot_repuesto; Type: DEFAULT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.ot_repuesto ALTER COLUMN id_ot_repuesto SET DEFAULT nextval('operativo."SEC_OT_REPUESTO_ID"'::regclass);


--
-- Name: permiso id_permiso; Type: DEFAULT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.permiso ALTER COLUMN id_permiso SET DEFAULT nextval('operativo."SEC_PERMISO_ID"'::regclass);


--
-- Name: repuesto id_repuesto; Type: DEFAULT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.repuesto ALTER COLUMN id_repuesto SET DEFAULT nextval('operativo."SEC_REPUESTO_ID"'::regclass);


--
-- Name: rol id_rol; Type: DEFAULT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.rol ALTER COLUMN id_rol SET DEFAULT nextval('operativo."SEC_ROL_ID"'::regclass);


--
-- Name: tipo_maquina id_tipo_maquina; Type: DEFAULT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.tipo_maquina ALTER COLUMN id_tipo_maquina SET DEFAULT nextval('operativo."SEC_TIPO_MAQUINA_ID"'::regclass);


--
-- Name: usuario id_usuario; Type: DEFAULT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.usuario ALTER COLUMN id_usuario SET DEFAULT nextval('operativo."SEC_USUARIO_ID"'::regclass);


--
-- Name: documentos id; Type: DEFAULT; Schema: vectorial; Owner: -
--

ALTER TABLE ONLY vectorial.documentos ALTER COLUMN id SET DEFAULT nextval('vectorial.documentos_id_seq'::regclass);


--
-- Name: fragmentos id; Type: DEFAULT; Schema: vectorial; Owner: -
--

ALTER TABLE ONLY vectorial.fragmentos ALTER COLUMN id SET DEFAULT nextval('vectorial.fragmentos_id_seq'::regclass);


--
-- Name: activo PK_ACTIVO; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.activo
    ADD CONSTRAINT "PK_ACTIVO" PRIMARY KEY (id_activo);


--
-- Name: cliente PK_CLIENTE; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.cliente
    ADD CONSTRAINT "PK_CLIENTE" PRIMARY KEY (id_cliente);


--
-- Name: estado_ot PK_ESTADO_OT; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.estado_ot
    ADD CONSTRAINT "PK_ESTADO_OT" PRIMARY KEY (id_estado_ot);


--
-- Name: historial_estado_ot PK_HISTORIAL_ESTADO_OT; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.historial_estado_ot
    ADD CONSTRAINT "PK_HISTORIAL_ESTADO_OT" PRIMARY KEY (id_h_estado_ot);


--
-- Name: historial_recomendaciones_ia PK_HISTORIAL_RECOMENDACIONES_IA; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.historial_recomendaciones_ia
    ADD CONSTRAINT "PK_HISTORIAL_RECOMENDACIONES_IA" PRIMARY KEY (id_recomendacion_ia);


--
-- Name: modelo_maquina PK_MODELO_MAQUINA; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.modelo_maquina
    ADD CONSTRAINT "PK_MODELO_MAQUINA" PRIMARY KEY (id_modelo_maquina);


--
-- Name: motivo_estado_ot PK_MOTIVO_ESTADO_OT; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.motivo_estado_ot
    ADD CONSTRAINT "PK_MOTIVO_ESTADO_OT" PRIMARY KEY (id_motivo_es);


--
-- Name: movimiento_stock PK_MOVIMIENTO_STOCK; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.movimiento_stock
    ADD CONSTRAINT "PK_MOVIMIENTO_STOCK" PRIMARY KEY (id_movimiento);


--
-- Name: orden_trabajo PK_ORDEN_TRABAJO; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.orden_trabajo
    ADD CONSTRAINT "PK_ORDEN_TRABAJO" PRIMARY KEY (id_orden);


--
-- Name: ot_repuesto PK_OT_REPUESTO; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.ot_repuesto
    ADD CONSTRAINT "PK_OT_REPUESTO" PRIMARY KEY (id_ot_repuesto);


--
-- Name: permiso PK_PERMISO; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.permiso
    ADD CONSTRAINT "PK_PERMISO" PRIMARY KEY (id_permiso);


--
-- Name: repuesto PK_REPUESTO; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.repuesto
    ADD CONSTRAINT "PK_REPUESTO" PRIMARY KEY (id_repuesto);


--
-- Name: rol PK_ROL; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.rol
    ADD CONSTRAINT "PK_ROL" PRIMARY KEY (id_rol);


--
-- Name: rol_permiso PK_ROL_PERMISO; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.rol_permiso
    ADD CONSTRAINT "PK_ROL_PERMISO" PRIMARY KEY (id_rol, id_permiso);


--
-- Name: tipo_maquina PK_TIPO_MAQUINA; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.tipo_maquina
    ADD CONSTRAINT "PK_TIPO_MAQUINA" PRIMARY KEY (id_tipo_maquina);


--
-- Name: usuario PK_USUARIO; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.usuario
    ADD CONSTRAINT "PK_USUARIO" PRIMARY KEY (id_usuario);


--
-- Name: usuario_rol PK_USUARIO_ROL; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.usuario_rol
    ADD CONSTRAINT "PK_USUARIO_ROL" PRIMARY KEY (id_usuario, id_rol);


--
-- Name: repuesto UK_CODIGO_REPUESTO; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.repuesto
    ADD CONSTRAINT "UK_CODIGO_REPUESTO" UNIQUE (codigo);


--
-- Name: cliente UK_CORREO_CLIENTE; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.cliente
    ADD CONSTRAINT "UK_CORREO_CLIENTE" UNIQUE (correo);


--
-- Name: usuario UK_MAIL_USUARIO; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.usuario
    ADD CONSTRAINT "UK_MAIL_USUARIO" UNIQUE (mail);


--
-- Name: cliente UK_NOMBRE_CLIENTE; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.cliente
    ADD CONSTRAINT "UK_NOMBRE_CLIENTE" UNIQUE (nombre);


--
-- Name: estado_ot UK_NOMBRE_ESTADO_OT; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.estado_ot
    ADD CONSTRAINT "UK_NOMBRE_ESTADO_OT" UNIQUE (nombre);


--
-- Name: permiso UK_NOMBRE_PERMISO; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.permiso
    ADD CONSTRAINT "UK_NOMBRE_PERMISO" UNIQUE (nombre);


--
-- Name: rol UK_NOMBRE_ROL; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.rol
    ADD CONSTRAINT "UK_NOMBRE_ROL" UNIQUE (nombre);


--
-- Name: tipo_maquina UK_NOMBRE_TIPO_MAQUINA; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.tipo_maquina
    ADD CONSTRAINT "UK_NOMBRE_TIPO_MAQUINA" UNIQUE (nombre);


--
-- Name: activo UK_NUM_SERIE_ACTIVO; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.activo
    ADD CONSTRAINT "UK_NUM_SERIE_ACTIVO" UNIQUE (num_serie);


--
-- Name: cliente UK_RAZON_SOCIAL_CLIENTE; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.cliente
    ADD CONSTRAINT "UK_RAZON_SOCIAL_CLIENTE" UNIQUE (razon_social);


--
-- Name: cliente UK_TELEFONO_CLIENTE; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.cliente
    ADD CONSTRAINT "UK_TELEFONO_CLIENTE" UNIQUE (telefono);


--
-- Name: usuario UK_TELEFONO_USUARIO; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.usuario
    ADD CONSTRAINT "UK_TELEFONO_USUARIO" UNIQUE (telefono);


--
-- Name: documento_tecnico documento_tecnico_pkey; Type: CONSTRAINT; Schema: vectorial; Owner: -
--

ALTER TABLE ONLY vectorial.documento_tecnico
    ADD CONSTRAINT documento_tecnico_pkey PRIMARY KEY (id);


--
-- Name: documentos documentos_pkey; Type: CONSTRAINT; Schema: vectorial; Owner: -
--

ALTER TABLE ONLY vectorial.documentos
    ADD CONSTRAINT documentos_pkey PRIMARY KEY (id);


--
-- Name: fragmentos fragmentos_documento_id_posicion_key; Type: CONSTRAINT; Schema: vectorial; Owner: -
--

ALTER TABLE ONLY vectorial.fragmentos
    ADD CONSTRAINT fragmentos_documento_id_posicion_key UNIQUE (documento_id, posicion);


--
-- Name: fragmentos fragmentos_pkey; Type: CONSTRAINT; Schema: vectorial; Owner: -
--

ALTER TABLE ONLY vectorial.fragmentos
    ADD CONSTRAINT fragmentos_pkey PRIMARY KEY (id);


--
-- Name: IDX_HISTORIAL_RECOMENDACIONES_IA_ORDEN; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IDX_HISTORIAL_RECOMENDACIONES_IA_ORDEN" ON operativo.historial_recomendaciones_ia USING btree (id_orden);


--
-- Name: IX_ACTIVO_ID_MODELO_MAQUINA; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_ACTIVO_ID_MODELO_MAQUINA" ON operativo.activo USING btree (id_modelo_maquina);


--
-- Name: IX_MODELO_MAQUINA_ID_TIPO_MAQUINA; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_MODELO_MAQUINA_ID_TIPO_MAQUINA" ON operativo.modelo_maquina USING btree (id_tipo_maquina);


--
-- Name: IX_MOVIMIENTO_STOCK_ID_USUARIO; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_MOVIMIENTO_STOCK_ID_USUARIO" ON operativo.movimiento_stock USING btree (id_usuario);


--
-- Name: IX_ORDEN_TRABAJO_ID_USUARIO_RESPONSABLE; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_ORDEN_TRABAJO_ID_USUARIO_RESPONSABLE" ON operativo.orden_trabajo USING btree (id_usuario_responsable);


--
-- Name: IX_ORDEN_TRABAJO_ID_USUARIO_TECNICO; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_ORDEN_TRABAJO_ID_USUARIO_TECNICO" ON operativo.orden_trabajo USING btree (id_usuario_tecnico);


--
-- Name: IX_ROL_PERMISO_ID_PERMISO; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_ROL_PERMISO_ID_PERMISO" ON operativo.rol_permiso USING btree (id_permiso);


--
-- Name: IX_USUARIO_ROL_ID_ROL; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_USUARIO_ROL_ID_ROL" ON operativo.usuario_rol USING btree (id_rol);


--
-- Name: fragmentos_documento_idx; Type: INDEX; Schema: vectorial; Owner: -
--

CREATE INDEX fragmentos_documento_idx ON vectorial.fragmentos USING btree (documento_id);


--
-- Name: fragmentos_texto_idx; Type: INDEX; Schema: vectorial; Owner: -
--

CREATE INDEX fragmentos_texto_idx ON vectorial.fragmentos USING gin (texto_busqueda);


--
-- Name: orden_trabajo FK_ACTIVO_ORDEN_TRABAJO; Type: FK CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.orden_trabajo
    ADD CONSTRAINT "FK_ACTIVO_ORDEN_TRABAJO" FOREIGN KEY (id_activo) REFERENCES operativo.activo(id_activo);


--
-- Name: activo FK_CLIENTE_ACTIVO; Type: FK CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.activo
    ADD CONSTRAINT "FK_CLIENTE_ACTIVO" FOREIGN KEY (id_cliente) REFERENCES operativo.cliente(id_cliente);


--
-- Name: orden_trabajo FK_CLIENTE_ORDEN_TRABAJO; Type: FK CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.orden_trabajo
    ADD CONSTRAINT "FK_CLIENTE_ORDEN_TRABAJO" FOREIGN KEY (id_cliente) REFERENCES operativo.cliente(id_cliente);


--
-- Name: historial_estado_ot FK_ESTADO_OT_HISTORIAL_ESTADO_OT; Type: FK CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.historial_estado_ot
    ADD CONSTRAINT "FK_ESTADO_OT_HISTORIAL_ESTADO_OT" FOREIGN KEY (id_estado_ot) REFERENCES operativo.estado_ot(id_estado_ot);


--
-- Name: activo FK_MODELO_MAQUINA_ACTIVO; Type: FK CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.activo
    ADD CONSTRAINT "FK_MODELO_MAQUINA_ACTIVO" FOREIGN KEY (id_modelo_maquina) REFERENCES operativo.modelo_maquina(id_modelo_maquina);


--
-- Name: historial_estado_ot FK_MOTIVO_ESTADO_OT_HISTORIAL_ESTADO_OT; Type: FK CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.historial_estado_ot
    ADD CONSTRAINT "FK_MOTIVO_ESTADO_OT_HISTORIAL_ESTADO_OT" FOREIGN KEY (id_motivo_es) REFERENCES operativo.motivo_estado_ot(id_motivo_es);


--
-- Name: historial_estado_ot FK_ORDEN_TRABAJO_HISTORIAL_ESTADO_OT; Type: FK CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.historial_estado_ot
    ADD CONSTRAINT "FK_ORDEN_TRABAJO_HISTORIAL_ESTADO_OT" FOREIGN KEY (id_orden) REFERENCES operativo.orden_trabajo(id_orden);


--
-- Name: historial_recomendaciones_ia FK_ORDEN_TRABAJO_HISTORIAL_RECOMENDACIONES_IA; Type: FK CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.historial_recomendaciones_ia
    ADD CONSTRAINT "FK_ORDEN_TRABAJO_HISTORIAL_RECOMENDACIONES_IA" FOREIGN KEY (id_orden) REFERENCES operativo.orden_trabajo(id_orden);


--
-- Name: ot_repuesto FK_ORDEN_TRABAJO_OT_REPUESTO; Type: FK CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.ot_repuesto
    ADD CONSTRAINT "FK_ORDEN_TRABAJO_OT_REPUESTO" FOREIGN KEY (id_orden) REFERENCES operativo.orden_trabajo(id_orden);


--
-- Name: rol_permiso FK_PERMISO_ROL_PERMISO; Type: FK CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.rol_permiso
    ADD CONSTRAINT "FK_PERMISO_ROL_PERMISO" FOREIGN KEY (id_permiso) REFERENCES operativo.permiso(id_permiso);


--
-- Name: orden_trabajo FK_RECOMENDACION_IA_ORDEN_TRABAJO; Type: FK CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.orden_trabajo
    ADD CONSTRAINT "FK_RECOMENDACION_IA_ORDEN_TRABAJO" FOREIGN KEY (id_recomendacion_ia) REFERENCES operativo.historial_recomendaciones_ia(id_recomendacion_ia);


--
-- Name: movimiento_stock FK_REPUESTO_MOVIMIENTO_STOCK; Type: FK CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.movimiento_stock
    ADD CONSTRAINT "FK_REPUESTO_MOVIMIENTO_STOCK" FOREIGN KEY (id_repuesto) REFERENCES operativo.repuesto(id_repuesto);


--
-- Name: ot_repuesto FK_REPUESTO_OT_REPUESTO; Type: FK CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.ot_repuesto
    ADD CONSTRAINT "FK_REPUESTO_OT_REPUESTO" FOREIGN KEY (id_repuesto) REFERENCES operativo.repuesto(id_repuesto);


--
-- Name: rol_permiso FK_ROL_ROL_PERMISO; Type: FK CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.rol_permiso
    ADD CONSTRAINT "FK_ROL_ROL_PERMISO" FOREIGN KEY (id_rol) REFERENCES operativo.rol(id_rol);


--
-- Name: usuario_rol FK_ROL_USUARIO_ROL; Type: FK CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.usuario_rol
    ADD CONSTRAINT "FK_ROL_USUARIO_ROL" FOREIGN KEY (id_rol) REFERENCES operativo.rol(id_rol);


--
-- Name: modelo_maquina FK_TIPO_MAQUINA_MODELO_MAQUINA; Type: FK CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.modelo_maquina
    ADD CONSTRAINT "FK_TIPO_MAQUINA_MODELO_MAQUINA" FOREIGN KEY (id_tipo_maquina) REFERENCES operativo.tipo_maquina(id_tipo_maquina);


--
-- Name: movimiento_stock FK_USUARIO_MOVIMIENTO_STOCK; Type: FK CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.movimiento_stock
    ADD CONSTRAINT "FK_USUARIO_MOVIMIENTO_STOCK" FOREIGN KEY (id_usuario) REFERENCES operativo.usuario(id_usuario);


--
-- Name: orden_trabajo FK_USUARIO_RESPONSABLE_ORDEN_TRABAJO; Type: FK CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.orden_trabajo
    ADD CONSTRAINT "FK_USUARIO_RESPONSABLE_ORDEN_TRABAJO" FOREIGN KEY (id_usuario_responsable) REFERENCES operativo.usuario(id_usuario);


--
-- Name: orden_trabajo FK_USUARIO_TECNICO_ORDEN_TRABAJO; Type: FK CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.orden_trabajo
    ADD CONSTRAINT "FK_USUARIO_TECNICO_ORDEN_TRABAJO" FOREIGN KEY (id_usuario_tecnico) REFERENCES operativo.usuario(id_usuario);


--
-- Name: usuario_rol FK_USUARIO_USUARIO_ROL; Type: FK CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.usuario_rol
    ADD CONSTRAINT "FK_USUARIO_USUARIO_ROL" FOREIGN KEY (id_usuario) REFERENCES operativo.usuario(id_usuario);


--
-- Name: fragmentos fragmentos_documento_id_fkey; Type: FK CONSTRAINT; Schema: vectorial; Owner: -
--

ALTER TABLE ONLY vectorial.fragmentos
    ADD CONSTRAINT fragmentos_documento_id_fkey FOREIGN KEY (documento_id) REFERENCES vectorial.documentos(id) ON DELETE CASCADE;


--
BEGIN;

INSERT INTO operativo.rol (nombre, descripcion, activo)
VALUES ('Jefe de mantenimiento', 'Gestión de mantenimiento y órdenes de trabajo', TRUE);

INSERT INTO operativo.permiso (nombre, descripcion)
VALUES
    ('activo.consultar', 'Consultar activos'),
    ('activo.crear', 'Registrar activos'),
    ('activo.editar', 'Modificar activos'),
    ('modelo.consultar', 'Consultar modelos de máquinas'),
    ('modelo.crear', 'Registrar modelos de máquinas'),
    ('ot.consultar', 'Consultar órdenes de trabajo'),
    ('ot.crear', 'Crear órdenes de trabajo'),
    ('ot.asignar_tecnico', 'Asignar un técnico a una orden de trabajo'),
    ('ot.registrar_avance', 'Registrar avances de una orden de trabajo');

INSERT INTO operativo.rol_permiso (id_rol, id_permiso)
SELECT rol.id_rol, permiso.id_permiso
FROM operativo.rol rol
CROSS JOIN operativo.permiso permiso
WHERE rol.nombre = 'Jefe de mantenimiento';

INSERT INTO operativo.usuario
    (nombre, apellido, mail, telefono, contrasena, estado, fecha_ingreso)
VALUES
    ('Jefe', 'Mantenimiento', 'jefe.mantenimiento@example.test', '000000000',
     'REEMPLAZAR_POR_HASH_ASPNET_IDENTITY', FALSE, CURRENT_DATE);

INSERT INTO operativo.usuario_rol (id_usuario, id_rol)
SELECT usuario.id_usuario, rol.id_rol
FROM operativo.usuario usuario
CROSS JOIN operativo.rol rol
WHERE usuario.mail = 'jefe.mantenimiento@example.test'
  AND rol.nombre = 'Jefe de mantenimiento';

COMMIT;

-- PostgreSQL database dump complete
--



