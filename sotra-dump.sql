--
-- PostgreSQL database dump
--

\restrict t3QTNlbS6vARwR03laPnlHnpvzn2gbSGbOtLZmD7xocIadd9f8vcUA5en57siqo

-- Dumped from database version 18.6 (Debian 18.6-1.pgdg13+2)
-- Dumped by pg_dump version 18.6 (Debian 18.6-1.pgdg13+2)

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
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
    CONSTRAINT "CK_ACTIVO_ANIO_FABRICACION" CHECK (((anio_fabricacion IS NULL) OR ((anio_fabricacion >= 1900) AND (anio_fabricacion <= 2100)))),
    CONSTRAINT "CK_ACTIVO_TEXTO_OBLIGATORIO" CHECK (((num_serie)::text ~ '[^[:space:]]'::text))
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
    fecha_ingreso date DEFAULT CURRENT_DATE NOT NULL,
    CONSTRAINT "CK_CLIENTE_TEXTOS_OBLIGATORIOS" CHECK ((((razon_social)::text ~ '[^[:space:]]'::text) AND ((nombre)::text ~ '[^[:space:]]'::text)))
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
    estado boolean CONSTRAINT estado_ot_activo_not_null NOT NULL,
    orden smallint NOT NULL,
    fase character varying(20) NOT NULL,
    exige_tecnico boolean DEFAULT false NOT NULL,
    completa_fecha character varying(10),
    tono character varying(12) NOT NULL,
    codigo_alerta character varying(40),
    es_inicial boolean DEFAULT false NOT NULL,
    CONSTRAINT "CK_ESTADO_OT_COMPLETA_FECHA" CHECK (((completa_fecha IS NULL) OR ((completa_fecha)::text = ANY ((ARRAY['inicio'::character varying, 'cierre'::character varying])::text[])))),
    CONSTRAINT "CK_ESTADO_OT_FASE" CHECK (((fase)::text = ANY ((ARRAY['pendiente'::character varying, 'en_curso'::character varying, 'por_validar'::character varying, 'cerrada'::character varying, 'cancelada'::character varying])::text[]))),
    CONSTRAINT "CK_ESTADO_OT_TONO" CHECK (((tono)::text = ANY ((ARRAY['neutro'::character varying, 'info'::character varying, 'alerta'::character varying, 'exito'::character varying, 'peligro'::character varying, 'destacado'::character varying])::text[])))
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
-- Name: evidencia_ot; Type: TABLE; Schema: operativo; Owner: -
--

CREATE TABLE operativo.evidencia_ot (
    id_evidencia bigint NOT NULL,
    id_orden bigint NOT NULL,
    id_usuario bigint,
    tipo character varying(20) NOT NULL,
    observacion text,
    fecha_registro date NOT NULL,
    creado_en timestamp with time zone DEFAULT now() NOT NULL,
    nombre_archivo character varying(255),
    tipo_contenido character varying(100),
    archivo bytea,
    CONSTRAINT "CK_EVIDENCIA_OT_TIPO" CHECK (((tipo)::text = ANY (ARRAY[('foto'::character varying)::text, ('documento'::character varying)::text, ('observacion'::character varying)::text])))
);


--
-- Name: SEC_EVIDENCIA_OT_ID; Type: SEQUENCE; Schema: operativo; Owner: -
--

CREATE SEQUENCE operativo."SEC_EVIDENCIA_OT_ID"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: SEC_EVIDENCIA_OT_ID; Type: SEQUENCE OWNED BY; Schema: operativo; Owner: -
--

ALTER SEQUENCE operativo."SEC_EVIDENCIA_OT_ID" OWNED BY operativo.evidencia_ot.id_evidencia;


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
    id_orden bigint,
    estado boolean DEFAULT true NOT NULL,
    sesion uuid,
    consulta text,
    numero_intento integer,
    fecha_generacion timestamp with time zone DEFAULT now() NOT NULL,
    resultado character varying(20) DEFAULT 'pendiente'::character varying NOT NULL,
    CONSTRAINT "CK_HISTORIAL_RECOMENDACIONES_IA_RESULTADO" CHECK (((resultado)::text = ANY (ARRAY[('pendiente'::character varying)::text, ('aceptada'::character varying)::text, ('descartada'::character varying)::text])))
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
    activo boolean DEFAULT true NOT NULL,
    CONSTRAINT "CK_MODELO_NOMBRE_OBLIGATORIO" CHECK (((nombre)::text ~ '[^[:space:]]'::text))
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
    fecha timestamp with time zone NOT NULL,
    cantidad integer NOT NULL,
    id_repuesto bigint NOT NULL,
    id_usuario bigint,
    tipo character varying(20) DEFAULT 'ingreso'::character varying NOT NULL,
    motivo character varying(200),
    ubicacion character varying(150),
    observaciones text,
    stock_anterior integer,
    stock_posterior integer,
    CONSTRAINT "CK_MOVIMIENTO_STOCK_TIPO" CHECK (((tipo)::text = ANY (ARRAY[('ingreso'::character varying)::text, ('egreso'::character varying)::text, ('ajuste'::character varying)::text])))
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
    CONSTRAINT "CK_ORDEN_DESCRIPCION_OBLIGATORIA" CHECK ((descripcion ~ '[^[:space:]]'::text)),
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
    cant_min integer,
    CONSTRAINT "CK_REPUESTO_CANTIDAD_NO_NEGATIVA" CHECK ((cantidad >= 0)),
    CONSTRAINT "CK_REPUESTO_CANT_MIN_NO_NEGATIVA" CHECK (((cant_min IS NULL) OR (cant_min >= 0))),
    CONSTRAINT "CK_REPUESTO_TEXTOS_OBLIGATORIOS" CHECK ((((codigo)::text ~ '[^[:space:]]'::text) AND ((nombre)::text ~ '[^[:space:]]'::text)))
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
    estado boolean NOT NULL,
    CONSTRAINT "CK_TIPO_NOMBRE_OBLIGATORIO" CHECK (((nombre)::text ~ '[^[:space:]]'::text))
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
    estado boolean NOT NULL,
    fecha_ingreso date DEFAULT CURRENT_DATE NOT NULL,
    id_keycloak uuid,
    CONSTRAINT "CK_USUARIO_TEXTOS_OBLIGATORIOS" CHECK ((((nombre)::text ~ '[^[:space:]]'::text) AND ((apellido)::text ~ '[^[:space:]]'::text) AND ((mail)::text ~ '[^[:space:]]'::text) AND ((telefono)::text ~ '[^[:space:]]'::text)))
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
-- Name: aceptacion_terminos; Type: TABLE; Schema: operativo; Owner: -
--

CREATE TABLE operativo.aceptacion_terminos (
    id_aceptacion bigint NOT NULL,
    id_usuario bigint NOT NULL,
    id_terminos bigint NOT NULL,
    aceptado_en timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: aceptacion_terminos_id_aceptacion_seq; Type: SEQUENCE; Schema: operativo; Owner: -
--

ALTER TABLE operativo.aceptacion_terminos ALTER COLUMN id_aceptacion ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME operativo.aceptacion_terminos_id_aceptacion_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: rol_permiso; Type: TABLE; Schema: operativo; Owner: -
--

CREATE TABLE operativo.rol_permiso (
    id_rol bigint NOT NULL,
    id_permiso bigint NOT NULL
);


--
-- Name: terminos_condiciones; Type: TABLE; Schema: operativo; Owner: -
--

CREATE TABLE operativo.terminos_condiciones (
    id_terminos bigint NOT NULL,
    version integer NOT NULL,
    titulo character varying(200) NOT NULL,
    texto text NOT NULL,
    datos_almacenados text NOT NULL,
    vigente boolean DEFAULT true NOT NULL,
    creado_en timestamp with time zone DEFAULT now() NOT NULL,
    id_usuario_autor bigint,
    CONSTRAINT "CK_TERMINOS_SIEMPRE_VIGENTE" CHECK (vigente),
    CONSTRAINT "CK_TERMINOS_VERSION_POSITIVA" CHECK ((version > 0))
);


--
-- Name: terminos_condiciones_id_terminos_seq; Type: SEQUENCE; Schema: operativo; Owner: -
--

ALTER TABLE operativo.terminos_condiciones ALTER COLUMN id_terminos ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME operativo.terminos_condiciones_id_terminos_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: transicion_estado_ot; Type: TABLE; Schema: operativo; Owner: -
--

CREATE TABLE operativo.transicion_estado_ot (
    id_estado_origen bigint NOT NULL,
    id_estado_destino bigint NOT NULL,
    exige_motivo boolean DEFAULT false NOT NULL,
    puede_tecnico boolean DEFAULT false NOT NULL,
    accion character varying(40),
    principal boolean DEFAULT false NOT NULL,
    al_asignar_tecnico boolean DEFAULT false NOT NULL,
    CONSTRAINT "CK_TRANSICION_ESTADO_OT_DISTINTOS" CHECK ((id_estado_origen <> id_estado_destino))
);


--
-- Name: usuario_rol; Type: TABLE; Schema: operativo; Owner: -
--

CREATE TABLE operativo.usuario_rol (
    id_usuario bigint NOT NULL,
    id_rol bigint NOT NULL
);


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
-- Name: evidencia_ot id_evidencia; Type: DEFAULT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.evidencia_ot ALTER COLUMN id_evidencia SET DEFAULT nextval('operativo."SEC_EVIDENCIA_OT_ID"'::regclass);


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
-- Data for Name: aceptacion_terminos; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.aceptacion_terminos (id_aceptacion, id_usuario, id_terminos, aceptado_en) FROM stdin;
1	1	1	2026-10-26 02:05:56.722322+00
2	3	1	2026-10-26 02:05:56.722322+00
3	4	1	2026-10-26 02:05:56.722322+00
4	5	1	2026-10-26 02:05:56.722322+00
5	6	1	2026-10-26 02:05:56.722322+00
\.


--
-- Data for Name: activo; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.activo (id_activo, id_cliente, num_serie, descripcion, id_modelo_maquina, anio_fabricacion, nombre, ubicacion, estado) FROM stdin;
1	1	SY215C-22-0481	Excavadora en obra vial.	1	2022	Excavadora SY215C #1	Obra Ruta 8 km 90	t
2	1	SY135C-23-0112	Excavadora compacta para zanjeo.	2	2023	Excavadora SY135C #1	Parque Industrial Treinta y Tres	t
3	2	SY365H-21-0033	Excavadora de gran porte.	3	2021	Excavadora SY365H	Obra Ruta 5 km 210	t
4	2	STG190C-22-0207	Motoniveladora de camino.	7	2022	Motoniveladora STG190C	Camino departamental, Durazno	t
5	2	SSR120C-23-0059	Rodillo de compactación.	8	2023	Rodillo SSR120C	Camino departamental, Durazno	t
6	3	SY5418-22-0304	Hormigonero de planta.	9	2022	Hormigonero SY5418 #1	Planta Canelones	t
7	3	SY5418-22-0305	Hormigonero de planta.	9	2022	Hormigonero SY5418 #2	Planta Canelones	t
8	4	SW405K-23-0140	Pala de carga en cantera.	5	2023	Pala cargadora SW405K	Cantera Sierra Chica	t
9	4	SKT20-21-0088	Volcador de cantera.	10	2021	Camión volcador SKT20	Cantera Sierra Chica	t
10	5	CAT420F-19-7715	Retroexcavadora rural.	4	2019	Retroexcavadora Cat 420F	Establecimiento en Rocha	t
11	5	SY135C-24-0201	Excavadora para obras de riego.	2	2024	Excavadora SY135C #2	Establecimiento en Rocha	t
12	1	SW305K-20-0377	Pala fuera de servicio.	6	2020	Pala cargadora SW305K	Depósito central	f
13	6	SY5418-24-0410	Hormigonero en guarda.	9	2024	Hormigonero SY5418 #3	Garaje en Montevideo	t
14	3	SY215C-23-0502	Excavadora de cantera.	1	2023	Excavadora SY215C #2	Cantera de Pando	t
\.


--
-- Data for Name: cliente; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.cliente (id_cliente, razon_social, nombre, correo, telefono, direccion, observaciones, estado, fecha_ingreso) FROM stdin;
1	Construcciones del Este S.A.	Marcelo Ibarra	marcelo.ibarra@construccionesdeleste.example.com	44435006	Ruta 8 km 132, Treinta y Tres	Cliente con contrato de mantenimiento anual.	t	2026-09-12
2	Vial Norte S.R.L.	Patricia Acosta	patricia.acosta@vialnorte.example.com	46221180	Av. Artigas 1450, Tacuarembó	Obras viales en el norte del país.	t	2026-09-12
3	Hormigones del Litoral S.A.	Gonzalo Techera	gonzalo.techera@hormigoneslitoral.example.com	22927741	Camino Maldonado km 21, Canelones	Coordinar visitas por la mañana.	t	2026-09-13
4	Cantera Sierra Chica	Ricardo Olivera	ricardo.olivera@sierrachica.example.com	45623390	Ruta 55 km 18, Colonia		t	2026-09-13
5	Agro Servicios Rocha	Natalia Cabrera	natalia.cabrera@agrorocha.example.com	44728815	Ruta 9 km 215, Rocha	Maquinaria en establecimientos rurales.	t	2026-09-14
6	Transportes Oriental Ltda.	Federico Barrios	federico.barrios@transportesoriental.example.com	24876620	Bulevar Artigas 2210, Montevideo	Cuenta suspendida a pedido del cliente.	f	2026-09-14
\.


--
-- Data for Name: estado_ot; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.estado_ot (id_estado_ot, nombre, descripcion, estado, orden, fase, exige_tecnico, completa_fecha, tono, codigo_alerta, es_inicial) FROM stdin;
1	creada	Creada	t	1	pendiente	f	\N	neutro	sin_asignar	t
2	en_ejecucion	En ejecución	t	3	en_curso	t	inicio	info	\N	f
3	asignada	Asignada	t	2	pendiente	t	\N	info	demorada_para_iniciar	f
4	pausada	Pausada	t	4	en_curso	f	\N	alerta	pausada_demasiado	f
5	finalizada	Finalizada	t	5	por_validar	f	\N	destacado	\N	f
6	cerrada	Cerrada	t	6	cerrada	f	cierre	exito	\N	f
7	cancelada	Cancelada	t	7	cancelada	f	\N	neutro	\N	f
\.


--
-- Data for Name: evidencia_ot; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.evidencia_ot (id_evidencia, id_orden, id_usuario, tipo, observacion, fecha_registro, creado_en, nombre_archivo, tipo_contenido, archivo) FROM stdin;
1	2	7	observacion	Se encontró el vástago con rayaduras leves; se recomienda revisar la alineación.	2026-09-22	2026-10-17 22:23:24.659427+00	\N	\N	\N
2	3	8	observacion	Se midió el desgaste de la cuchilla antes del cambio: 14 mm.	2026-09-27	2026-10-17 22:23:24.659427+00	\N	\N	\N
3	5	6	observacion	Se aísla la fuga en la manguera del cilindro de levante.	2026-10-05	2026-10-17 22:23:24.659427+00	\N	\N	\N
4	10	7	observacion	La válvula de freno de servicio pierde presión; se continúa con la prueba de hermeticidad.	2026-10-12	2026-10-17 22:23:24.659427+00	\N	\N	\N
\.


--
-- Data for Name: historial_estado_ot; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.historial_estado_ot (id_h_estado_ot, id_orden, id_motivo_es, id_estado_ot, fecha_hora, observacion) FROM stdin;
1	1	\N	1	2026-09-14 09:10:00+00	\N
2	1	\N	3	2026-09-14 12:10:00+00	Técnico asignado: Laura Gómez
3	1	\N	2	2026-09-16 08:30:00+00	\N
4	1	\N	5	2026-09-16 17:40:00+00	Trabajo completado.
5	1	\N	6	2026-09-16 19:40:00+00	Trabajo validado.
6	2	\N	1	2026-09-20 10:00:00+00	\N
7	2	\N	3	2026-09-20 13:00:00+00	Técnico asignado: Roberto Pérez
8	2	\N	2	2026-09-22 09:00:00+00	\N
9	2	\N	5	2026-09-23 16:30:00+00	Trabajo completado.
10	3	\N	1	2026-09-25 08:20:00+00	\N
11	3	\N	3	2026-09-25 11:20:00+00	Técnico asignado: Diego Fernández
12	3	\N	2	2026-09-27 08:00:00+00	\N
13	3	\N	5	2026-09-29 15:00:00+00	Trabajo completado.
14	3	\N	6	2026-09-29 17:00:00+00	Trabajo validado.
15	4	\N	1	2026-10-01 11:00:00+00	\N
16	4	\N	3	2026-10-01 14:00:00+00	Técnico asignado: Andrés Rodríguez
17	4	\N	2	2026-10-02 08:00:00+00	\N
18	4	\N	5	2026-10-02 12:30:00+00	Trabajo completado.
19	4	\N	6	2026-10-02 14:30:00+00	Trabajo validado.
20	5	\N	1	2026-10-03 09:40:00+00	\N
21	5	\N	3	2026-10-03 12:40:00+00	Técnico asignado: Laura Gómez
22	5	\N	2	2026-10-05 08:15:00+00	\N
23	6	\N	1	2026-10-05 10:30:00+00	\N
24	6	\N	3	2026-10-05 13:30:00+00	Técnico asignado: Roberto Pérez
25	7	\N	1	2026-10-06 13:00:00+00	\N
26	7	\N	3	2026-10-06 16:00:00+00	Técnico asignado: Diego Fernández
27	7	\N	2	2026-10-07 09:00:00+00	\N
28	7	\N	4	2026-10-07 11:00:00+00	Esperando autorización del cliente para reemplazar la batería.
29	8	\N	1	2026-10-08 09:00:00+00	\N
30	9	\N	1	2026-10-09 15:20:00+00	\N
31	9	\N	3	2026-10-09 18:20:00+00	Técnico asignado: Andrés Rodríguez
32	10	\N	1	2026-10-10 08:45:00+00	\N
33	10	\N	3	2026-10-10 11:45:00+00	Técnico asignado: Roberto Pérez
34	10	\N	2	2026-10-12 08:30:00+00	\N
35	11	\N	1	2026-10-12 10:10:00+00	\N
36	11	\N	3	2026-10-12 13:10:00+00	Técnico asignado: Laura Gómez
37	11	\N	2	2026-10-14 08:00:00+00	\N
38	11	\N	5	2026-10-14 13:20:00+00	Trabajo completado.
39	12	\N	1	2026-10-13 09:30:00+00	\N
40	13	\N	1	2026-10-14 16:00:00+00	\N
41	13	\N	3	2026-10-14 19:00:00+00	Técnico asignado: Diego Fernández
42	14	\N	1	2026-10-15 08:10:00+00	\N
\.


--
-- Data for Name: historial_recomendaciones_ia; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.historial_recomendaciones_ia (id_recomendacion_ia, recomendacion, id_orden, estado, sesion, consulta, numero_intento, fecha_generacion, resultado) FROM stdin;
1	Revisar el estado de los terminales y la carga de la batería antes de reemplazarla.	7	t	a1c0f4de-3b1f-4c1b-9d7a-1b0f6a8e0001	batería sin carga no arranca	1	2026-10-06 13:20:00+00	pendiente
2	Aislar el circuito hidráulico y verificar mangueras y racores en busca de fugas.	5	t	a1c0f4de-3b1f-4c1b-9d7a-1b0f6a8e0002	falla sistema hidráulico pala	1	2026-10-03 09:55:00+00	aceptada
3	Verificar la válvula de protección del circuito y la presión del compresor.	10	t	a1c0f4de-3b1f-4c1b-9d7a-1b0f6a8e0003	pérdida de presión de aire frenos	1	2026-10-10 09:05:00+00	aceptada
4	Cambiar el aceite del motor sin revisar el estado de los filtros.	11	t	a1c0f4de-3b1f-4c1b-9d7a-1b0f6a8e0004	cambio de aceite y filtros	1	2026-10-12 10:30:00+00	descartada
\.


--
-- Data for Name: modelo_maquina; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.modelo_maquina (id_modelo_maquina, nombre, id_tipo_maquina, descripcion, activo) FROM stdin;
1	SANY SY215C	1	Excavadora hidráulica de 21 toneladas.	t
2	SANY SY135C	1	Excavadora hidráulica compacta de 13 toneladas.	t
3	SANY SY365H	1	Excavadora hidráulica de gran porte.	t
4	Caterpillar 420F	2	Retroexcavadora con cargador frontal.	t
5	SANY SW405K	3	Pala cargadora de 5 toneladas.	t
6	SANY SW305K	3	Pala cargadora de 3 toneladas.	f
7	SANY STG190C	4	Motoniveladora de 190 HP.	t
8	SANY SSR120C	5	Rodillo vibrocompactador de 12 toneladas.	t
9	SANY SY5418GJB	6	Camión hormigonero de 8 m³.	t
10	SANY SKT20	7	Camión volcador de obra.	t
\.


--
-- Data for Name: motivo_estado_ot; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.motivo_estado_ot (id_motivo_es, descripcion) FROM stdin;
\.


--
-- Data for Name: movimiento_stock; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.movimiento_stock (id_movimiento, fecha, cantidad, id_repuesto, id_usuario, tipo, motivo, ubicacion, observaciones, stock_anterior, stock_posterior) FROM stdin;
1	2026-09-12 10:05:00+00	4	5	9	ingreso	Compra a proveedor	Depósito B-1	\N	0	4
2	2026-09-12 10:10:00+00	10	10	9	ingreso	Compra a proveedor	Depósito A-3	\N	0	10
3	2026-09-13 10:01:00+00	20	1	9	ingreso	Compra a proveedor	Depósito A-1	\N	0	20
4	2026-09-13 10:06:00+00	9	6	9	ingreso	Compra a proveedor	Depósito C-3	\N	0	9
5	2026-09-13 10:11:00+00	6	11	9	ingreso	Compra a proveedor	Depósito C-4	\N	0	6
6	2026-09-14 10:02:00+00	5	2	9	ingreso	Compra a proveedor	Depósito A-1	\N	0	5
7	2026-09-14 10:07:00+00	3	7	9	ingreso	Compra a proveedor	Depósito C-1	\N	0	3
8	2026-09-14 10:12:00+00	8	12	9	ingreso	Compra a proveedor	Depósito C-5	\N	0	8
9	2026-09-15 10:03:00+00	13	3	9	ingreso	Compra a proveedor	Depósito A-2	\N	0	13
10	2026-09-15 10:08:00+00	68	8	9	ingreso	Compra a proveedor	Depósito C-2	\N	0	68
11	2026-09-15 10:13:00+00	52	13	9	ingreso	Compra a proveedor	Depósito B-2	\N	0	52
12	2026-09-16 09:30:00+00	1	1	9	egreso	Consumo en la orden de trabajo #1	Depósito A-1	\N	20	19
13	2026-09-16 10:04:00+00	10	4	9	ingreso	Compra a proveedor	Depósito B-1	\N	0	10
14	2026-09-16 10:09:00+00	2	9	9	ingreso	Compra a proveedor	Depósito D-1	\N	0	2
15	2026-09-16 10:30:00+00	1	3	9	egreso	Consumo en la orden de trabajo #1	Depósito A-2	\N	13	12
16	2026-09-16 11:30:00+00	2	5	9	egreso	Consumo en la orden de trabajo #1	Depósito B-1	\N	4	2
17	2026-09-22 10:00:00+00	1	11	9	egreso	Consumo en la orden de trabajo #2	Depósito C-4	\N	6	5
18	2026-09-22 11:00:00+00	1	4	9	egreso	Consumo en la orden de trabajo #2	Depósito B-1	\N	10	9
19	2026-09-22 12:00:00+00	1	6	9	egreso	Consumo en la orden de trabajo #2	Depósito C-3	\N	9	8
20	2026-09-27 09:00:00+00	2	12	9	egreso	Consumo en la orden de trabajo #3	Depósito C-5	\N	8	6
21	2026-09-27 10:00:00+00	8	8	9	egreso	Consumo en la orden de trabajo #3	Depósito C-2	\N	68	60
22	2026-10-02 09:00:00+00	4	13	9	egreso	Consumo en la orden de trabajo #4	Depósito B-2	\N	52	48
23	2026-10-05 09:15:00+00	1	6	9	egreso	Consumo en la orden de trabajo #5	Depósito C-3	\N	8	7
24	2026-10-14 09:00:00+00	1	1	9	egreso	Consumo en la orden de trabajo #11	Depósito A-1	\N	19	18
25	2026-10-14 10:00:00+00	2	5	9	egreso	Consumo en la orden de trabajo #11	Depósito B-1	\N	2	0
26	2026-10-14 11:00:00+00	1	2	9	egreso	Consumo en la orden de trabajo #11	Depósito A-1	\N	5	4
\.


--
-- Data for Name: orden_trabajo; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.orden_trabajo (id_orden, id_activo, id_cliente, tipo, descripcion, fecha_creada, fecha_inicio, fecha_cierre, resultado, id_usuario_responsable, id_usuario_tecnico, prioridad, id_recomendacion_ia) FROM stdin;
1	1	1	t	Mantenimiento preventivo de 500 horas: cambio de aceite y filtros.	2026-09-14 09:10:00+00	2026-09-16 08:30:00+00	2026-09-16 19:40:00+00	Se cambiaron aceite y filtros. Sin novedades.	5	6	3	\N
2	3	2	f	Pérdida de aceite en el cilindro del brazo.	2026-09-20 10:00:00+00	2026-09-22 09:00:00+00	\N	Se reemplazaron los sellos del cilindro y se repuso aceite.	5	7	5	\N
3	4	2	f	Cuchilla de la motoniveladora desgastada.	2026-09-25 08:20:00+00	2026-09-27 08:00:00+00	2026-09-29 17:00:00+00	Se cambió la cuchilla y se ajustó la fijación.	4	8	3	\N
4	6	3	t	Revisión y engrase del tambor mezclador.	2026-10-01 11:00:00+00	2026-10-02 08:00:00+00	2026-10-02 14:30:00+00	Engrase completo y revisión de rodillos de apoyo.	5	10	1	\N
6	9	4	t	Cambio de filtros de aire y combustible del volcador.	2026-10-05 10:30:00+00	\N	\N		5	7	3	\N
7	10	5	f	La batería no tiene carga y la máquina no arranca.	2026-10-06 13:00:00+00	2026-10-07 09:00:00+00	\N		4	8	5	\N
8	2	1	t	Inspección general de 250 horas.	2026-10-08 09:00:00+00	\N	\N		5	\N	3	\N
9	5	2	f	Vibración excesiva del rodillo en funcionamiento.	2026-10-09 15:20:00+00	\N	\N		4	10	5	\N
11	11	5	t	Cambio de aceite de motor y filtros.	2026-10-12 10:10:00+00	2026-10-14 08:00:00+00	\N	Servicio realizado según el plan. Pendiente de validación.	4	6	3	\N
12	14	3	f	Desgaste de los dientes del balde.	2026-10-13 09:30:00+00	\N	\N		5	\N	3	\N
13	13	6	t	Revisión previa a la temporada de trabajo.	2026-10-14 16:00:00+00	\N	\N		5	8	1	\N
14	1	1	f	Ruido anormal en el mando final.	2026-10-15 08:10:00+00	\N	\N		4	\N	5	\N
5	8	4	f	Falla en el sistema hidráulico de la pala.	2026-10-03 09:40:00+00	2026-10-05 08:15:00+00	\N		4	6	5	2
10	7	3	f	Pérdida de presión de aire en el circuito de frenos.	2026-10-10 08:45:00+00	2026-10-12 08:30:00+00	\N		5	7	5	3
\.


--
-- Data for Name: ot_repuesto; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.ot_repuesto (id_ot_repuesto, id_orden, id_repuesto) FROM stdin;
1	1	1
2	1	3
3	1	5
4	2	11
5	2	4
6	2	6
7	3	12
8	3	8
9	4	13
10	5	6
11	11	1
12	11	5
13	11	2
\.


--
-- Data for Name: permiso; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.permiso (id_permiso, nombre, descripcion) FROM stdin;
1	usuario.consultar	Consultar usuarios
2	usuario.crear	Crear usuarios
3	usuario.editar	Modificar usuarios
4	activo.consultar	Consultar activos
5	activo.crear	Registrar activos
6	activo.editar	Modificar activos
7	modelo.consultar	Consultar modelos de máquinas
8	modelo.crear	Registrar modelos de máquinas
9	ot.consultar	Consultar órdenes de trabajo
10	ot.crear	Crear órdenes de trabajo
11	ot.asignar_tecnico	Asignar un técnico a una orden de trabajo
12	ot.registrar_avance	Registrar avances de una orden de trabajo
13	stock.consultar	Consultar stock
14	stock.registrar_movimiento	Registrar movimientos de stock
15	terminos.administrar	Definir el texto de los términos y condiciones
\.


--
-- Data for Name: repuesto; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.repuesto (codigo, nombre, descripcion, cantidad, estado, ubicacion, marca, id_repuesto, cant_min) FROM stdin;
FIL-ACE-215	Filtro de aceite de motor	Para excavadoras SY215C y SY135C.	18	t	Depósito A-1	SANY	1	6
FIL-AIR-215	Filtro de aire primario	Elemento primario del filtro de aire.	4	t	Depósito A-1	Donaldson	2	5
FIL-COMB-215	Filtro de combustible	Separador de agua y combustible.	12	t	Depósito A-2	Baldwin	3	4
ACE-HID-46	Aceite hidráulico ISO 46 (balde de 20 L)	\N	9	t	Depósito B-1	Shell	4	4
ACE-MOT-15W40	Aceite de motor 15W-40 (balde de 20 L)	\N	0	t	Depósito B-1	Shell	5	3
MAN-HID-12	Manguera hidráulica de 1/2 pulgada	Con terminales prensados.	7	t	Depósito C-3	Gates	6	2
DIE-BALDE-SY	Diente de balde de excavadora	\N	3	t	Depósito C-1	ESCO	7	8
BUL-RUE-M20	Bulón de rueda M20	Calidad 10.9.	60	t	Depósito C-2	\N	8	20
BAT-12V-150	Batería de 12 V y 150 Ah	\N	2	t	Depósito D-1	Moura	9	2
COR-ALT-DS	Correa de alternador	\N	10	t	Depósito A-3	Gates	10	\N
SEL-CIL-SY	Kit de sellos de cilindro hidráulico	Incluye retenes y o-rings.	5	t	Depósito C-4	SANY	11	2
LAM-CUC-400	Cuchilla para motoniveladora	\N	6	t	Depósito C-5	SANY	12	4
GRA-EP2-400	Grasa EP2 (cartucho de 400 g)	\N	48	t	Depósito B-2	Shell	13	24
LUZ-TRA-LED	Faro de trabajo LED	Descontinuado.	0	f	Depósito D-2	Hella	14	2
\.


--
-- Data for Name: rol; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.rol (id_rol, nombre, descripcion, activo) FROM stdin;
1	Administrador	Administración general del sistema	t
2	Jefe de mantenimiento	Gestión de mantenimiento y órdenes de trabajo	t
3	Técnico	Ejecución y registro de tareas de mantenimiento	t
4	Pañolero	Gestión de repuestos y movimientos de stock	t
\.


--
-- Data for Name: rol_permiso; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.rol_permiso (id_rol, id_permiso) FROM stdin;
1	1
1	2
1	3
1	4
1	5
1	6
1	7
1	8
1	9
1	10
1	11
1	12
1	13
1	14
2	4
2	5
2	6
2	7
2	8
2	9
2	10
2	11
2	12
3	4
3	7
3	9
3	12
4	4
4	9
4	13
4	14
1	15
\.


--
-- Data for Name: terminos_condiciones; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.terminos_condiciones (id_terminos, version, titulo, texto, datos_almacenados, vigente, creado_en, id_usuario_autor) FROM stdin;
1	1	Términos y condiciones de uso de SOTRA	SOTRA es la plataforma de gestión de mantenimiento de CVM. Al usarla aceptás estos términos.\n\nUso de tus datos: los datos personales que se guardan en SOTRA se usan únicamente para gestionar tu cuenta, asignarte tareas y registrar quién hizo cada acción dentro del sistema. No se venden, no se ceden a terceros ni se usan para publicidad ni para fines ajenos al mantenimiento.\n\nMarco legal: el tratamiento de tus datos personales se rige por la Ley N.º 18.331 de Protección de Datos Personales de Uruguay.\n\nTus derechos: podés pedir que se corrijan tus datos o que se dé de baja tu cuenta. La baja conserva el historial de trabajo (órdenes, evidencias y movimientos) para no perder la trazabilidad, pero deja de permitir tu acceso.\n\nSeguridad: el acceso es personal; no compartas tus credenciales.	Nombre y apellido\nCorreo electrónico\nTeléfono\nRol o roles asignados\nEstado de la cuenta y fecha de ingreso\nContraseña (no se guarda en SOTRA: la gestiona Keycloak y nunca se muestra)\nRegistro de tus acciones en el sistema (qué hiciste, sobre qué recurso, cuándo y con qué resultado)\nFotos y archivos que adjuntes como evidencia en una orden de trabajo\nFecha y versión de los términos que aceptaste	t	2026-10-26 02:05:56.63712+00	\N
\.


--
-- Data for Name: tipo_maquina; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.tipo_maquina (id_tipo_maquina, nombre, estado) FROM stdin;
1	Excavadora hidráulica	t
2	Retroexcavadora	t
3	Pala cargadora	t
4	Motoniveladora	t
5	Rodillo compactador	t
6	Camión hormigonero	t
7	Camión volcador	t
\.


--
-- Data for Name: transicion_estado_ot; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.transicion_estado_ot (id_estado_origen, id_estado_destino, exige_motivo, puede_tecnico, accion, principal, al_asignar_tecnico) FROM stdin;
1	3	f	f	\N	f	t
1	7	t	f	\N	f	f
3	2	f	t	Iniciar	t	f
3	7	t	f	\N	f	f
2	4	t	t	Pausar	f	f
2	5	f	t	Finalizar	t	f
4	2	f	t	Reanudar	t	f
4	7	t	f	\N	f	f
5	6	t	f	\N	f	f
5	3	t	f	\N	f	f
\.


--
-- Data for Name: usuario; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.usuario (id_usuario, nombre, apellido, mail, telefono, estado, fecha_ingreso, id_keycloak) FROM stdin;
1	Kevin	Moreira	kmoreira@gmail.com	99583488	t	2026-09-12	\N
3	Nicolas	Diaz	nicodiaz@gmail.com	99568742	t	2026-09-12	\N
4	Cris	Romero	crisrome@gmail.com	99653147	t	2026-09-12	\N
5	Martín	Silva	martin.silva@example.com	99111205	t	2026-09-14	\N
6	Laura	Gómez	laura.gomez@example.com	99222316	t	2026-09-14	\N
7	Roberto	Pérez	roberto.perez@example.com	99333427	t	2026-09-15	\N
8	Diego	Fernández	diego.fernandez@example.com	99444538	t	2026-09-15	\N
9	Lucía	Méndez	lucia.mendez@example.com	99555649	t	2026-09-16	\N
10	Andrés	Rodríguez	andres.rodriguez@example.com	99666750	t	2026-09-16	\N
11	Camila	Ferreira	camila.ferreira@example.com	99777861	f	2026-09-17	\N
12	Admin	Prueba	admin.prueba@sotra.test	99900101	t	2026-10-17	\N
13	Jefe	Prueba	jefe.prueba@sotra.test	99900102	t	2026-10-17	\N
14	Técnico	Prueba	tecnico.prueba@sotra.test	99900103	t	2026-10-17	\N
15	Pañolero	Prueba	panolero.prueba@sotra.test	99900104	t	2026-10-17	\N
\.


--
-- Data for Name: usuario_rol; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.usuario_rol (id_usuario, id_rol) FROM stdin;
1	1
3	1
4	2
5	2
6	3
7	3
8	3
9	4
10	3
11	3
12	1
13	2
14	3
15	4
\.


--
-- Name: SEC_ACTIVO_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_ACTIVO_ID"', 14, true);


--
-- Name: SEC_CLIENTE_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_CLIENTE_ID"', 6, true);


--
-- Name: SEC_ESTADO_OT_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_ESTADO_OT_ID"', 7, true);


--
-- Name: SEC_EVIDENCIA_OT_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_EVIDENCIA_OT_ID"', 4, true);


--
-- Name: SEC_HISTORIAL_ESTADO_OT_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_HISTORIAL_ESTADO_OT_ID"', 42, true);


--
-- Name: SEC_HISTORIAL_RECOMENDACIONES_IA_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_HISTORIAL_RECOMENDACIONES_IA_ID"', 4, true);


--
-- Name: SEC_MODELO_MAQUINA_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_MODELO_MAQUINA_ID"', 10, true);


--
-- Name: SEC_MOTIVO_ESTADO_OT_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_MOTIVO_ESTADO_OT_ID"', 1, false);


--
-- Name: SEC_MOVIMIENTO_STOCK_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_MOVIMIENTO_STOCK_ID"', 26, true);


--
-- Name: SEC_ORDEN_TRABAJO_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_ORDEN_TRABAJO_ID"', 14, true);


--
-- Name: SEC_OT_REPUESTO_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_OT_REPUESTO_ID"', 13, true);


--
-- Name: SEC_PERMISO_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_PERMISO_ID"', 15, true);


--
-- Name: SEC_REPUESTO_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_REPUESTO_ID"', 14, true);


--
-- Name: SEC_ROL_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_ROL_ID"', 4, true);


--
-- Name: SEC_TIPO_MAQUINA_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_TIPO_MAQUINA_ID"', 7, true);


--
-- Name: SEC_USUARIO_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_USUARIO_ID"', 15, true);


--
-- Name: aceptacion_terminos_id_aceptacion_seq; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo.aceptacion_terminos_id_aceptacion_seq', 5, true);


--
-- Name: terminos_condiciones_id_terminos_seq; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo.terminos_condiciones_id_terminos_seq', 1, true);


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
-- Name: evidencia_ot PK_EVIDENCIA_OT; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.evidencia_ot
    ADD CONSTRAINT "PK_EVIDENCIA_OT" PRIMARY KEY (id_evidencia);


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
-- Name: transicion_estado_ot PK_TRANSICION_ESTADO_OT; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.transicion_estado_ot
    ADD CONSTRAINT "PK_TRANSICION_ESTADO_OT" PRIMARY KEY (id_estado_origen, id_estado_destino);


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
-- Name: aceptacion_terminos UK_ACEPTACION_USUARIO_TERMINOS; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.aceptacion_terminos
    ADD CONSTRAINT "UK_ACEPTACION_USUARIO_TERMINOS" UNIQUE (id_usuario, id_terminos);


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
-- Name: usuario UK_ID_KEYCLOAK_USUARIO; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.usuario
    ADD CONSTRAINT "UK_ID_KEYCLOAK_USUARIO" UNIQUE (id_keycloak);


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
-- Name: terminos_condiciones UK_TERMINOS_VERSION; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.terminos_condiciones
    ADD CONSTRAINT "UK_TERMINOS_VERSION" UNIQUE (version);


--
-- Name: aceptacion_terminos aceptacion_terminos_pkey; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.aceptacion_terminos
    ADD CONSTRAINT aceptacion_terminos_pkey PRIMARY KEY (id_aceptacion);


--
-- Name: terminos_condiciones terminos_condiciones_pkey; Type: CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.terminos_condiciones
    ADD CONSTRAINT terminos_condiciones_pkey PRIMARY KEY (id_terminos);


--
-- Name: IDX_HISTORIAL_RECOMENDACIONES_IA_ORDEN; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IDX_HISTORIAL_RECOMENDACIONES_IA_ORDEN" ON operativo.historial_recomendaciones_ia USING btree (id_orden);


--
-- Name: IX_ACEPTACION_TERMINOS_ID_TERMINOS; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_ACEPTACION_TERMINOS_ID_TERMINOS" ON operativo.aceptacion_terminos USING btree (id_terminos);


--
-- Name: IX_ACTIVO_ID_CLIENTE; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_ACTIVO_ID_CLIENTE" ON operativo.activo USING btree (id_cliente);


--
-- Name: IX_ACTIVO_ID_MODELO_MAQUINA; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_ACTIVO_ID_MODELO_MAQUINA" ON operativo.activo USING btree (id_modelo_maquina);


--
-- Name: IX_EVIDENCIA_OT_ID_ORDEN; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_EVIDENCIA_OT_ID_ORDEN" ON operativo.evidencia_ot USING btree (id_orden);


--
-- Name: IX_HISTORIAL_ESTADO_OT_ESTADO; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_HISTORIAL_ESTADO_OT_ESTADO" ON operativo.historial_estado_ot USING btree (id_estado_ot);


--
-- Name: IX_HISTORIAL_ESTADO_OT_ORDEN_FECHA; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_HISTORIAL_ESTADO_OT_ORDEN_FECHA" ON operativo.historial_estado_ot USING btree (id_orden, fecha_hora DESC, id_h_estado_ot DESC);


--
-- Name: IX_HISTORIAL_RECOMENDACIONES_IA_ID_ORDEN; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_HISTORIAL_RECOMENDACIONES_IA_ID_ORDEN" ON operativo.historial_recomendaciones_ia USING btree (id_orden);


--
-- Name: IX_HISTORIAL_RECOMENDACIONES_IA_SESION; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_HISTORIAL_RECOMENDACIONES_IA_SESION" ON operativo.historial_recomendaciones_ia USING btree (sesion);


--
-- Name: IX_MODELO_MAQUINA_ID_TIPO_MAQUINA; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_MODELO_MAQUINA_ID_TIPO_MAQUINA" ON operativo.modelo_maquina USING btree (id_tipo_maquina);


--
-- Name: IX_MOVIMIENTO_STOCK_FECHA; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_MOVIMIENTO_STOCK_FECHA" ON operativo.movimiento_stock USING btree (fecha DESC, id_movimiento DESC);


--
-- Name: IX_MOVIMIENTO_STOCK_ID_REPUESTO; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_MOVIMIENTO_STOCK_ID_REPUESTO" ON operativo.movimiento_stock USING btree (id_repuesto);


--
-- Name: IX_MOVIMIENTO_STOCK_ID_USUARIO; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_MOVIMIENTO_STOCK_ID_USUARIO" ON operativo.movimiento_stock USING btree (id_usuario);


--
-- Name: IX_ORDEN_TRABAJO_FECHA_CREADA; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_ORDEN_TRABAJO_FECHA_CREADA" ON operativo.orden_trabajo USING btree (fecha_creada DESC, id_orden DESC);


--
-- Name: IX_ORDEN_TRABAJO_ID_ACTIVO; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_ORDEN_TRABAJO_ID_ACTIVO" ON operativo.orden_trabajo USING btree (id_activo);


--
-- Name: IX_ORDEN_TRABAJO_ID_CLIENTE; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_ORDEN_TRABAJO_ID_CLIENTE" ON operativo.orden_trabajo USING btree (id_cliente);


--
-- Name: IX_ORDEN_TRABAJO_ID_USUARIO_RESPONSABLE; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_ORDEN_TRABAJO_ID_USUARIO_RESPONSABLE" ON operativo.orden_trabajo USING btree (id_usuario_responsable);


--
-- Name: IX_ORDEN_TRABAJO_ID_USUARIO_TECNICO; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_ORDEN_TRABAJO_ID_USUARIO_TECNICO" ON operativo.orden_trabajo USING btree (id_usuario_tecnico);


--
-- Name: IX_OT_REPUESTO_ID_ORDEN; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_OT_REPUESTO_ID_ORDEN" ON operativo.ot_repuesto USING btree (id_orden);


--
-- Name: IX_OT_REPUESTO_ID_REPUESTO; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_OT_REPUESTO_ID_REPUESTO" ON operativo.ot_repuesto USING btree (id_repuesto);


--
-- Name: IX_ROL_PERMISO_ID_PERMISO; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_ROL_PERMISO_ID_PERMISO" ON operativo.rol_permiso USING btree (id_permiso);


--
-- Name: IX_USUARIO_ROL_ID_ROL; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_USUARIO_ROL_ID_ROL" ON operativo.usuario_rol USING btree (id_rol);


--
-- Name: UK_ESTADO_OT_INICIAL; Type: INDEX; Schema: operativo; Owner: -
--

CREATE UNIQUE INDEX "UK_ESTADO_OT_INICIAL" ON operativo.estado_ot USING btree (es_inicial) WHERE es_inicial;


--
-- Name: UK_ESTADO_OT_ORDEN; Type: INDEX; Schema: operativo; Owner: -
--

CREATE UNIQUE INDEX "UK_ESTADO_OT_ORDEN" ON operativo.estado_ot USING btree (orden);


--
-- Name: UK_TELEFONO_CLIENTE_NORMALIZADO; Type: INDEX; Schema: operativo; Owner: -
--

CREATE UNIQUE INDEX "UK_TELEFONO_CLIENTE_NORMALIZADO" ON operativo.cliente USING btree ((
CASE
    WHEN ((length((telefono)::text) = 9) AND ("left"((telefono)::text, 1) = '0'::text)) THEN (SUBSTRING(telefono FROM 2))::character varying
    ELSE telefono
END));


--
-- Name: UK_TELEFONO_USUARIO_NORMALIZADO; Type: INDEX; Schema: operativo; Owner: -
--

CREATE UNIQUE INDEX "UK_TELEFONO_USUARIO_NORMALIZADO" ON operativo.usuario USING btree ((
CASE
    WHEN ((length((telefono)::text) = 9) AND ("left"((telefono)::text, 1) = '0'::text)) THEN (SUBSTRING(telefono FROM 2))::character varying
    ELSE telefono
END));


--
-- Name: UK_TERMINOS_UNICA_VIGENTE; Type: INDEX; Schema: operativo; Owner: -
--

CREATE UNIQUE INDEX "UK_TERMINOS_UNICA_VIGENTE" ON operativo.terminos_condiciones USING btree (vigente) WHERE vigente;


--
-- Name: UK_TRANSICION_AL_ASIGNAR_TECNICO; Type: INDEX; Schema: operativo; Owner: -
--

CREATE UNIQUE INDEX "UK_TRANSICION_AL_ASIGNAR_TECNICO" ON operativo.transicion_estado_ot USING btree (id_estado_origen) WHERE al_asignar_tecnico;


--
-- Name: UX_MODELO_MAQUINA_NOMBRE_TIPO; Type: INDEX; Schema: operativo; Owner: -
--

CREATE UNIQUE INDEX "UX_MODELO_MAQUINA_NOMBRE_TIPO" ON operativo.modelo_maquina USING btree (lower((nombre)::text), id_tipo_maquina);


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
-- Name: evidencia_ot FK_ORDEN_TRABAJO_EVIDENCIA_OT; Type: FK CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.evidencia_ot
    ADD CONSTRAINT "FK_ORDEN_TRABAJO_EVIDENCIA_OT" FOREIGN KEY (id_orden) REFERENCES operativo.orden_trabajo(id_orden);


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
-- Name: transicion_estado_ot FK_TRANSICION_ESTADO_OT_DESTINO; Type: FK CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.transicion_estado_ot
    ADD CONSTRAINT "FK_TRANSICION_ESTADO_OT_DESTINO" FOREIGN KEY (id_estado_destino) REFERENCES operativo.estado_ot(id_estado_ot);


--
-- Name: transicion_estado_ot FK_TRANSICION_ESTADO_OT_ORIGEN; Type: FK CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.transicion_estado_ot
    ADD CONSTRAINT "FK_TRANSICION_ESTADO_OT_ORIGEN" FOREIGN KEY (id_estado_origen) REFERENCES operativo.estado_ot(id_estado_ot);


--
-- Name: evidencia_ot FK_USUARIO_EVIDENCIA_OT; Type: FK CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.evidencia_ot
    ADD CONSTRAINT "FK_USUARIO_EVIDENCIA_OT" FOREIGN KEY (id_usuario) REFERENCES operativo.usuario(id_usuario);


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
-- Name: aceptacion_terminos aceptacion_terminos_id_terminos_fkey; Type: FK CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.aceptacion_terminos
    ADD CONSTRAINT aceptacion_terminos_id_terminos_fkey FOREIGN KEY (id_terminos) REFERENCES operativo.terminos_condiciones(id_terminos);


--
-- Name: aceptacion_terminos aceptacion_terminos_id_usuario_fkey; Type: FK CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.aceptacion_terminos
    ADD CONSTRAINT aceptacion_terminos_id_usuario_fkey FOREIGN KEY (id_usuario) REFERENCES operativo.usuario(id_usuario);


--
-- Name: terminos_condiciones terminos_condiciones_id_usuario_autor_fkey; Type: FK CONSTRAINT; Schema: operativo; Owner: -
--

ALTER TABLE ONLY operativo.terminos_condiciones
    ADD CONSTRAINT terminos_condiciones_id_usuario_autor_fkey FOREIGN KEY (id_usuario_autor) REFERENCES operativo.usuario(id_usuario);


--
-- PostgreSQL database dump complete
--

\unrestrict t3QTNlbS6vARwR03laPnlHnpvzn2gbSGbOtLZmD7xocIadd9f8vcUA5en57siqo

