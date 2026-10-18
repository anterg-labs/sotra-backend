-- Esquema operativo con sus datos (sin documentos tecnicos ni embeddings).
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

CREATE EXTENSION IF NOT EXISTS vector WITH SCHEMA public;

DROP SCHEMA IF EXISTS operativo CASCADE;
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
    CONSTRAINT "CK_REPUESTO_CANT_MIN_NO_NEGATIVA" CHECK (((cant_min IS NULL) OR (cant_min >= 0)))
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
-- Data for Name: activo; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.activo (id_activo, id_cliente, num_serie, descripcion, id_modelo_maquina, anio_fabricacion, nombre, ubicacion, estado) FROM stdin;
1	6	E2E-1790468098	Activo temporal editado	5	2025	Activo E2E Editado 1790468098	Banco editado	t
2	4	UI-1790468130	Alta realizada desde el frontend	6	2026	Activo UI Editado 1790468130	Prueba de interfaz	t
\.


--
-- Data for Name: cliente; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.cliente (id_cliente, razon_social, nombre, correo, telefono, direccion, observaciones, estado, fecha_ingreso) FROM stdin;
4	ANTER-G	Los antiguos LOGGOS	micoorre@gmail.com.uy	0996586321	Mi casa		t	2026-09-20
1	Depósito Este S.A.	Laura Gómez	laura.gomez@depositoeste.com	+598 4443 5006	Ruta 8 km 132, Treinta y Tres	Nueva coordinación por teléfono.	t	2026-09-19
6	PRUEBA E2E EDITADA 1790468098	Contacto 1790468098	\N	\N	Ubicacion temporal	Registro temporal de integracion	t	2026-09-27
5	ANTER-G2	Los antiguos LOGGOSs	micoorre@gmail.com	099586478	Mi casa 2	Nuevos	t	2026-09-22
\.


--
-- Data for Name: estado_ot; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.estado_ot (id_estado_ot, nombre, descripcion, estado) FROM stdin;
1	creada	\N	t
2	en_ejecucion	\N	t
3	asignada	\N	t
4	pausada	\N	t
5	finalizada	\N	t
6	cerrada	\N	t
\.


--
-- Data for Name: evidencia_ot; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.evidencia_ot (id_evidencia, id_orden, id_usuario, tipo, observacion, fecha_registro, creado_en, nombre_archivo, tipo_contenido, archivo) FROM stdin;
1	6	\N	observacion	Se revis� el equipo	2026-10-09	2026-10-09 03:53:45.770532+00	\N	\N	\N
2	6	\N	foto		2026-10-09	2026-10-09 03:53:45.945869+00	foto.png	image/png	\\x89504e470d0a1a0a30303030
\.


--
-- Data for Name: historial_estado_ot; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.historial_estado_ot (id_h_estado_ot, id_orden, id_motivo_es, id_estado_ot, fecha_hora, observacion) FROM stdin;
5	3	\N	1	2026-09-28 04:35:13.107417+00	2026-10-06
6	3	\N	3	2026-09-28 04:35:13.397678+00	\N
7	4	\N	1	2026-09-29 01:11:28.808562+00	2026-09-29
8	5	\N	1	2026-10-08 03:04:18.40544+00	2026-12-01
9	5	\N	3	2026-10-08 03:04:18.654234+00	Técnico asignado: Roberto Pérez
10	5	\N	2	2026-10-08 03:04:18.806171+00	\N
11	5	\N	4	2026-10-08 03:04:18.963477+00	Falta de repuestos
12	5	\N	2	2026-10-08 03:04:19.034376+00	\N
13	5	\N	5	2026-10-08 03:04:19.093809+00	\N
14	5	\N	6	2026-10-08 03:04:19.240695+00	Trabajo validado
15	6	\N	1	2026-10-08 03:06:54.686553+00	2026-12-02
16	6	\N	3	2026-10-08 03:07:08.341421+00	Técnico asignado: Roberto Pérez
17	6	\N	2	2026-10-08 03:07:13.382346+00	
18	6	\N	4	2026-10-08 03:07:26.174248+00	Falta de repuestos
19	7	\N	1	2026-10-09 03:58:00.91145+00	2026-12-05
\.


--
-- Data for Name: historial_recomendaciones_ia; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.historial_recomendaciones_ia (id_recomendacion_ia, recomendacion, id_orden, estado, sesion, consulta, numero_intento, fecha_generacion, resultado) FROM stdin;
5	Mantenimiento cada 2000 horas. Los mantenimientos correspondientes a las 250, 500 y 1000 horas se deben realizar al mismo tiempo.\nSe debe preparar una manija y colocar la marca "TOP" con los tapones (P) y (F) perpendicular al suelo. Luego, se deben quitar los tapones (P) y (F) con una llave hexagonal para drenar el aceite en un recipiente debajo de la salida (P). Se debe comprobar que los anillos-O de los tapones no estén dañados; si lo están, se deben reemplazar. Después, se aprieta el tapón (P) y se añade aceite a través del orificio del tapón (F), hasta que éste rebote por el mismo, luego se coloca el tapón (F). La torsión de apriete para los tapones (P) y (F) debe ser de 68.6 ± 9.8 N·m. Se añade aceite a través del orificio más fácil de llenar que no tenga engranajes internos.\n\nAdvertencias:\n- Inmediatamente después de parar el motor, las partes y el aceite se encuentran a temperatura muy alta. Por esta razón, si usted toca el aceite o las partes inmediatamente después de haber detenido el motor, usted sufrirá quemaduras. Espere a que baje la temperatura antes de iniciar la operación.\n- Si queda aceite a presión dentro de la caja, el aceite o el tapón pueden salir proyectados. Afloje el tapón suavemente para dejar salir la presión. Para evitar peligros, no se pare directamente en frente del tapón.\n\nAclaración:\nLa consulta puede referirse a más de un procedimiento del manual. Esta respuesta corresponde a "Cambiar el aceite en la caja del mando final". También se encontraron:\n- Cambiar el aceite del cárter de aceite del motor, sustituir el elemento del filtro del aceite del motor (página 304)\n- Cambiar el aceite de la caja de la maquinaria de giro (página 316)\nIndique cuál necesita para obtener una respuesta precisa.\n\nFuente:\nManual Operacion y Mantenimiento PC200-8 (3).pdf, página 321, sección "Cambiar el aceite en la caja del mando final".	4	t	\N	\N	\N	2026-10-09 03:53:36.514788+00	aceptada
6	Se debe comprobar el indicador de obstrucción del filtro de aire situado en el panel de control, y si este se enciende intermitentemente, se debe limpiar el elemento del filtro de aire. Si tras un año de uso o después de la limpieza el monitor de obstrucción del purificador de aire del tablero de instrumentos se enciende nuevamente, se deben reemplazar el elemento externo (5), el elemento interno (6) y el anillo-O- (8). También se debe sustituir la válvula del vaciador si está dañada o si el caucho se encuentra notablemente deformado.\n\nAdvertencias:\n- Al utilizar aire comprimido, existe el peligro de que la suciedad salga despedida y provoque lesiones graves. Utilice siempre gafas de seguridad, máscara para el polvo, y otros equipos de protección.\n- Es peligroso extraer por la fuerza el elemento externo del cuerpo del filtro de aire. Al trabajar en lugares elevados o en lugares con poca superficie de apoyo para los pies, tenga cuidado de no caer como consecuencia del tirón realizado para sacar el elemento externo.\n- No limpie el elemento de la depuradora de aire mientras el indicador luminoso de obstrucción de la depuradora de aire, en el panel de indicadores, no destella.\n- Si la inspección, limpieza o mantenimiento se realiza con el motor en operación, entrará suciedad en el motor y provocará daños en éste. Pare siempre el motor cuando efectúe estas operaciones.\n\nFuente:\nManual Operacion y Mantenimiento PC200-8 (3).pdf, página 273, sección "Mantenimiento cuando sea necesario comprobar, limpiar y cambiar el elemento del filtro de aire".	4	t	\N	\N	\N	2026-10-09 03:53:36.514788+00	aceptada
8	Texto B	6	t	\N	\N	\N	2026-10-09 03:53:36.514788+00	aceptada
7	Texto A	6	f	\N	\N	\N	2026-10-09 03:53:36.514788+00	aceptada
9	Texto C	6	t	\N	\N	\N	2026-10-09 03:53:36.514788+00	aceptada
10	Sugerencia de prueba A	7	f	11111111-2222-4333-8444-555555555555	cambiar filtro	1	2026-10-09 03:57:41.958335+00	descartada
11	Sugerencia de prueba B	7	t	11111111-2222-4333-8444-555555555555	cambiar filtro	2	2026-10-09 03:57:41.958335+00	aceptada
\.


--
-- Data for Name: modelo_maquina; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.modelo_maquina (id_modelo_maquina, nombre, id_tipo_maquina, descripcion, activo) FROM stdin;
5	Modelo E2E 1790468098	9	Modelo temporal de integracion	t
6	Modelo UI 1790468130	10	Modelo temporal para formulario	t
9	Prueba modelo temporal	13	\N	t
4	Caterpillar 3200	10	Excavadora hidráulica sobre orugas.	t
\.


--
-- Data for Name: motivo_estado_ot; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.motivo_estado_ot (id_motivo_es, descripcion) FROM stdin;
\.


--
-- Data for Name: movimiento_stock; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.movimiento_stock (id_movimiento, estado, fecha, cantidad, id_repuesto, id_usuario, tipo, motivo, ubicacion, observaciones, stock_anterior, stock_posterior) FROM stdin;
1	t	2026-10-09 03:53:53.913338+00	5	4	\N	ingreso	Compra a proveedor	Deposito A	\N	1	6
\.


--
-- Data for Name: orden_trabajo; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.orden_trabajo (id_orden, id_activo, id_cliente, tipo, descripcion, fecha_creada, fecha_inicio, fecha_cierre, resultado, id_usuario_responsable, id_usuario_tecnico, prioridad, id_recomendacion_ia) FROM stdin;
3	2	4	f	Orden de prueba de simplificacion	2026-09-28 04:35:13.043609+00	\N	\N		1	\N	3	\N
4	1	6	t	cambiar filtro de aire	2026-09-29 01:11:28.741624+00	\N	\N		1	\N	3	\N
5	2	4	f	Orden de prueba de transiciones de estado	2026-10-08 03:04:18.298161+00	2026-10-08 03:04:18.806383+00	2026-10-08 03:04:19.240696+00		4	25	1	\N
6	2	4	t	Orden de prueba del detalle con acciones	2026-10-08 03:06:54.682136+00	2026-10-08 03:07:13.382349+00	\N		4	25	5	\N
7	2	4	f	Orden de prueba del registro de sugerencias	2026-10-09 03:58:00.768957+00	\N	\N		4	\N	1	\N
\.


--
-- Data for Name: ot_repuesto; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.ot_repuesto (id_ot_repuesto, id_orden, id_repuesto) FROM stdin;
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
\.


--
-- Data for Name: repuesto; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.repuesto (codigo, nombre, descripcion, cantidad, estado, ubicacion, marca, id_repuesto, cant_min) FROM stdin;
PRUEBA-SIMPLIF-1	Repuesto de prueba	\N	1	f	\N	\N	3	0
PRUEBA-STOCK-1	Repuesto de prueba de stock	Prueba de ingresos	6	t	Deposito A	Prueba	4	3
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
\.


--
-- Data for Name: tipo_maquina; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.tipo_maquina (id_tipo_maquina, nombre, estado) FROM stdin;
8	Retroexcavadora	t
7	Excavadora hidráulica	t
9	Tipo E2E 1790468098	t
10	Tipo UI 1790468130	t
13	Prueba tipo temporal	t
\.


--
-- Data for Name: usuario; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.usuario (id_usuario, nombre, apellido, mail, telefono, contrasena, estado, fecha_ingreso) FROM stdin;
1	Kevin	Moreira	kmoreira@gmail.com	099583488	REEMPLAZAR_POR_HASH_ASPNET_IDENTITY	t	2026-09-12
4	Cris	Romero	crisrome@gmail.com	099653147	REEMPLAZAR_POR_HASH_ASPNET_IDENTITY	t	2026-09-12
6	Laura	Gómez	laura@gmail.com	099123455	AQAAAAIAAYagAAAAEHNDgaqhKNxxchlL47hWM5TJ3psI88eIDKv4PiJeoUrFZbtXhbEdUpicRlMBehq0pQ==	f	2026-09-18
3	Nicolas	Diaz	nicodiaz@gmail.com	099568742	REEMPLAZAR_POR_HASH_ASPNET_IDENTITY	f	2026-09-12
9	Prueba Editada	Integracion	prueba.integracion.1790468098@example.test	TEST-1790468098	AQAAAAIAAYagAAAAEAlewkMamJGMVQ1x5gpxjIm6x/Nbyco1TKFPeg4coY71aT9DwMDBAvIR4V2t4VlcCQ==	f	2026-09-27
8	Lauraa	Gómez	laura@example.com	094755648	AQAAAAIAAYagAAAAECcm6SQC8rtGTjyaxob61TR2Fqncx3RXISply7LekLqSc3kADp7740RZH15IrL/wYQ==	f	2026-09-19
25	Roberto	Pérez	juan.perez@example.com	092222222	AQAAAAIAAYagAAAAEIlsrsAUwvWDQdwREI2zSHuROC1ndE7gBEIpgAV9iIrIgcmJe0IK/JsYbLUZh5m5nQ==	t	2026-10-06
\.


--
-- Data for Name: usuario_rol; Type: TABLE DATA; Schema: operativo; Owner: -
--

COPY operativo.usuario_rol (id_usuario, id_rol) FROM stdin;
1	1
3	1
4	2
6	1
6	3
8	1
8	3
9	1
25	3
\.


--
-- Name: SEC_ACTIVO_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_ACTIVO_ID"', 4, true);


--
-- Name: SEC_CLIENTE_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_CLIENTE_ID"', 6, true);


--
-- Name: SEC_ESTADO_OT_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_ESTADO_OT_ID"', 6, true);


--
-- Name: SEC_EVIDENCIA_OT_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_EVIDENCIA_OT_ID"', 2, true);


--
-- Name: SEC_HISTORIAL_ESTADO_OT_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_HISTORIAL_ESTADO_OT_ID"', 19, true);


--
-- Name: SEC_HISTORIAL_RECOMENDACIONES_IA_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_HISTORIAL_RECOMENDACIONES_IA_ID"', 11, true);


--
-- Name: SEC_MODELO_MAQUINA_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_MODELO_MAQUINA_ID"', 9, true);


--
-- Name: SEC_MOTIVO_ESTADO_OT_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_MOTIVO_ESTADO_OT_ID"', 1, false);


--
-- Name: SEC_MOVIMIENTO_STOCK_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_MOVIMIENTO_STOCK_ID"', 1, true);


--
-- Name: SEC_ORDEN_TRABAJO_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_ORDEN_TRABAJO_ID"', 7, true);


--
-- Name: SEC_OT_REPUESTO_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_OT_REPUESTO_ID"', 4, true);


--
-- Name: SEC_PERMISO_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_PERMISO_ID"', 14, true);


--
-- Name: SEC_REPUESTO_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_REPUESTO_ID"', 4, true);


--
-- Name: SEC_ROL_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_ROL_ID"', 4, true);


--
-- Name: SEC_TIPO_MAQUINA_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_TIPO_MAQUINA_ID"', 14, true);


--
-- Name: SEC_USUARIO_ID; Type: SEQUENCE SET; Schema: operativo; Owner: -
--

SELECT pg_catalog.setval('operativo."SEC_USUARIO_ID"', 25, true);


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
-- Name: IDX_HISTORIAL_RECOMENDACIONES_IA_ORDEN; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IDX_HISTORIAL_RECOMENDACIONES_IA_ORDEN" ON operativo.historial_recomendaciones_ia USING btree (id_orden);


--
-- Name: IX_ACTIVO_ID_MODELO_MAQUINA; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_ACTIVO_ID_MODELO_MAQUINA" ON operativo.activo USING btree (id_modelo_maquina);


--
-- Name: IX_EVIDENCIA_OT_ID_ORDEN; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_EVIDENCIA_OT_ID_ORDEN" ON operativo.evidencia_ot USING btree (id_orden);


--
-- Name: IX_HISTORIAL_RECOMENDACIONES_IA_SESION; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_HISTORIAL_RECOMENDACIONES_IA_SESION" ON operativo.historial_recomendaciones_ia USING btree (sesion);


--
-- Name: IX_MODELO_MAQUINA_ID_TIPO_MAQUINA; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_MODELO_MAQUINA_ID_TIPO_MAQUINA" ON operativo.modelo_maquina USING btree (id_tipo_maquina);


--
-- Name: IX_MOVIMIENTO_STOCK_ID_REPUESTO; Type: INDEX; Schema: operativo; Owner: -
--

CREATE INDEX "IX_MOVIMIENTO_STOCK_ID_REPUESTO" ON operativo.movimiento_stock USING btree (id_repuesto);


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
-- Terminos y condiciones (2026-10-17)
--

-- Términos y condiciones con aceptación por persona usuaria.
-- Es aditivo e idempotente: solo crea tablas nuevas, un permiso y la versión inicial del texto.
-- Aplicar a mano a las bases existentes. Las bases nuevas lo traen en database/init/01-init.sql.

CREATE TABLE IF NOT EXISTS operativo.terminos_condiciones (
    id_terminos bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    version integer NOT NULL,
    titulo character varying(200) NOT NULL,
    texto text NOT NULL,
    datos_almacenados text NOT NULL,
    vigente boolean DEFAULT true NOT NULL,
    creado_en timestamp with time zone DEFAULT now() NOT NULL,
    id_usuario_autor bigint REFERENCES operativo.usuario (id_usuario),
    CONSTRAINT "UK_TERMINOS_VERSION" UNIQUE (version),
    CONSTRAINT "CK_TERMINOS_VERSION_POSITIVA" CHECK (version > 0),
    CONSTRAINT "CK_TERMINOS_SIEMPRE_VIGENTE" CHECK (vigente)
);

-- A lo sumo una versión vigente.
CREATE UNIQUE INDEX IF NOT EXISTS "UK_TERMINOS_UNICA_VIGENTE" ON operativo.terminos_condiciones (vigente) WHERE vigente;

CREATE TABLE IF NOT EXISTS operativo.aceptacion_terminos (
    id_aceptacion bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    id_usuario bigint NOT NULL REFERENCES operativo.usuario (id_usuario),
    id_terminos bigint NOT NULL REFERENCES operativo.terminos_condiciones (id_terminos),
    aceptado_en timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT "UK_ACEPTACION_USUARIO_TERMINOS" UNIQUE (id_usuario, id_terminos)
);

CREATE INDEX IF NOT EXISTS "IX_ACEPTACION_TERMINOS_ID_TERMINOS" ON operativo.aceptacion_terminos (id_terminos);

-- Permiso para definir el texto (se asigna al rol Administrador).
INSERT INTO operativo.permiso (nombre, descripcion)
SELECT 'terminos.administrar', 'Definir el texto de los términos y condiciones'
WHERE NOT EXISTS (SELECT 1 FROM operativo.permiso WHERE nombre = 'terminos.administrar');

INSERT INTO operativo.rol_permiso (id_rol, id_permiso)
SELECT r.id_rol, p.id_permiso
FROM operativo.rol r, operativo.permiso p
WHERE r.nombre = 'Administrador' AND p.nombre = 'terminos.administrar'
  AND NOT EXISTS (SELECT 1 FROM operativo.rol_permiso rp WHERE rp.id_rol = r.id_rol AND rp.id_permiso = p.id_permiso);

-- Versión inicial provisoria: debe revisarla el equipo antes de producción.
INSERT INTO operativo.terminos_condiciones (version, titulo, texto, datos_almacenados, vigente)
SELECT 1,
       'Términos y condiciones de uso de SOTRA',
       E'SOTRA es la plataforma de gestión de mantenimiento de CVM. Al usarla aceptás estos términos.\n\n'
       || E'Uso de tus datos: los datos personales que se guardan en SOTRA se usan únicamente para gestionar tu cuenta, '
       || E'asignarte tareas y registrar quién hizo cada acción dentro del sistema. No se venden, no se ceden a terceros ni se '
       || E'usan para publicidad ni para fines ajenos al mantenimiento.\n\n'
       || E'Tus derechos: podés pedir que se corrijan tus datos o que se dé de baja tu cuenta. La baja conserva el historial '
       || E'de trabajo (órdenes, evidencias y movimientos) para no perder la trazabilidad, pero deja de permitir tu acceso.\n\n'
       || E'Seguridad: el acceso es personal; no compartas tus credenciales.',
       E'Nombre y apellido\nCorreo electrónico\nTeléfono\nRol o roles asignados\nEstado de la cuenta y fecha de ingreso\n'
       || E'Contraseña (se guarda protegida y nunca se muestra; el inicio de sesión se integrará con Keycloak)\n'
       || E'Registro de tus acciones en el sistema (qué hiciste, sobre qué recurso, cuándo y con qué resultado)\n'
       || E'Fotos y archivos que adjuntes como evidencia en una orden de trabajo\n'
       || E'Fecha y versión de los términos que aceptaste',
       true
WHERE NOT EXISTS (SELECT 1 FROM operativo.terminos_condiciones);
