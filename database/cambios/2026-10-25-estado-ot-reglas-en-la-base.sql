-- Las reglas de los estados de las órdenes pasan de estar escritas en el código a estar en la base: orden, fase,
-- si exige técnico, qué fecha completa, tono de color, código de alerta y estado inicial, más la tabla de
-- transiciones (quién puede pasar a quién, si exige motivo, qué puede hacer un técnico y cómo se llama el botón).
-- Idempotente; lo aplica el despliegue (cargar-datos.sh). Si hay estados que este cambio no conoce, se detiene
-- con un mensaje claro en lugar de inventarles reglas.

ALTER TABLE operativo.estado_ot
    ADD COLUMN IF NOT EXISTS orden smallint,
    ADD COLUMN IF NOT EXISTS fase character varying(20),
    ADD COLUMN IF NOT EXISTS exige_tecnico boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS completa_fecha character varying(10),
    ADD COLUMN IF NOT EXISTS tono character varying(12),
    ADD COLUMN IF NOT EXISTS codigo_alerta character varying(40),
    ADD COLUMN IF NOT EXISTS es_inicial boolean NOT NULL DEFAULT false;

-- Los siete estados y sus etiquetas tienen que existir antes de definir sus reglas (no depende del orden de los archivos).
INSERT INTO operativo.estado_ot (nombre, descripcion, estado)
SELECT v.nombre, v.etiqueta, true
FROM (VALUES ('creada', 'Creada'), ('asignada', 'Asignada'), ('en_ejecucion', 'En ejecución'), ('pausada', 'Pausada'),
             ('finalizada', 'Finalizada'), ('cerrada', 'Cerrada'), ('cancelada', 'Cancelada')) AS v(nombre, etiqueta)
WHERE NOT EXISTS (SELECT 1 FROM operativo.estado_ot e WHERE e.nombre = v.nombre);

UPDATE operativo.estado_ot AS e
SET descripcion = v.etiqueta
FROM (VALUES ('creada', 'Creada'), ('asignada', 'Asignada'), ('en_ejecucion', 'En ejecución'), ('pausada', 'Pausada'),
             ('finalizada', 'Finalizada'), ('cerrada', 'Cerrada'), ('cancelada', 'Cancelada')) AS v(nombre, etiqueta)
WHERE e.nombre = v.nombre AND (e.descripcion IS NULL OR btrim(e.descripcion) = '');

UPDATE operativo.estado_ot AS e
SET orden = v.orden, fase = v.fase, exige_tecnico = v.exige_tecnico, completa_fecha = v.completa_fecha,
    tono = v.tono, codigo_alerta = v.codigo_alerta, es_inicial = v.es_inicial
FROM (VALUES
    ('creada', 1, 'pendiente', false, NULL, 'neutro', 'sin_asignar', true),
    ('asignada', 2, 'pendiente', true, NULL, 'info', 'demorada_para_iniciar', false),
    ('en_ejecucion', 3, 'en_curso', true, 'inicio', 'info', NULL, false),
    ('pausada', 4, 'en_curso', false, NULL, 'alerta', 'pausada_demasiado', false),
    ('finalizada', 5, 'por_validar', false, NULL, 'destacado', NULL, false),
    ('cerrada', 6, 'cerrada', false, 'cierre', 'exito', NULL, false),
    ('cancelada', 7, 'cancelada', false, NULL, 'neutro', NULL, false)
) AS v(nombre, orden, fase, exige_tecnico, completa_fecha, tono, codigo_alerta, es_inicial)
WHERE e.nombre = v.nombre AND e.fase IS NULL;

DO $reglas$
DECLARE
    desconocidos text;
BEGIN
    SELECT string_agg(nombre, ', ') INTO desconocidos FROM operativo.estado_ot WHERE fase IS NULL OR orden IS NULL OR tono IS NULL;
    IF desconocidos IS NOT NULL THEN
        RAISE EXCEPTION 'Hay estados de órdenes sin reglas (%): definir su orden, fase y tono antes de aplicar este cambio', desconocidos;
    END IF;

    ALTER TABLE operativo.estado_ot ALTER COLUMN orden SET NOT NULL, ALTER COLUMN fase SET NOT NULL, ALTER COLUMN tono SET NOT NULL;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'operativo.estado_ot'::regclass AND conname = 'CK_ESTADO_OT_FASE') THEN
        ALTER TABLE operativo.estado_ot ADD CONSTRAINT "CK_ESTADO_OT_FASE"
            CHECK (fase IN ('pendiente', 'en_curso', 'por_validar', 'cerrada', 'cancelada'));
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'operativo.estado_ot'::regclass AND conname = 'CK_ESTADO_OT_COMPLETA_FECHA') THEN
        ALTER TABLE operativo.estado_ot ADD CONSTRAINT "CK_ESTADO_OT_COMPLETA_FECHA"
            CHECK (completa_fecha IS NULL OR completa_fecha IN ('inicio', 'cierre'));
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'operativo.estado_ot'::regclass AND conname = 'CK_ESTADO_OT_TONO') THEN
        ALTER TABLE operativo.estado_ot ADD CONSTRAINT "CK_ESTADO_OT_TONO"
            CHECK (tono IN ('neutro', 'info', 'alerta', 'exito', 'peligro', 'destacado'));
    END IF;
END
$reglas$;

CREATE UNIQUE INDEX IF NOT EXISTS "UK_ESTADO_OT_ORDEN" ON operativo.estado_ot (orden);
CREATE UNIQUE INDEX IF NOT EXISTS "UK_ESTADO_OT_INICIAL" ON operativo.estado_ot (es_inicial) WHERE es_inicial;

CREATE TABLE IF NOT EXISTS operativo.transicion_estado_ot (
    id_estado_origen bigint NOT NULL,
    id_estado_destino bigint NOT NULL,
    exige_motivo boolean NOT NULL DEFAULT false,
    puede_tecnico boolean NOT NULL DEFAULT false,
    accion character varying(40),
    principal boolean NOT NULL DEFAULT false,
    al_asignar_tecnico boolean NOT NULL DEFAULT false,
    CONSTRAINT "PK_TRANSICION_ESTADO_OT" PRIMARY KEY (id_estado_origen, id_estado_destino),
    CONSTRAINT "FK_TRANSICION_ESTADO_OT_ORIGEN" FOREIGN KEY (id_estado_origen) REFERENCES operativo.estado_ot (id_estado_ot),
    CONSTRAINT "FK_TRANSICION_ESTADO_OT_DESTINO" FOREIGN KEY (id_estado_destino) REFERENCES operativo.estado_ot (id_estado_ot),
    CONSTRAINT "CK_TRANSICION_ESTADO_OT_DISTINTOS" CHECK (id_estado_origen <> id_estado_destino)
);

CREATE UNIQUE INDEX IF NOT EXISTS "UK_TRANSICION_AL_ASIGNAR_TECNICO" ON operativo.transicion_estado_ot (id_estado_origen) WHERE al_asignar_tecnico;

-- Ciclo de vida de la sección 13.1 de «Flujo de datos SOTRA.md».
INSERT INTO operativo.transicion_estado_ot
    (id_estado_origen, id_estado_destino, exige_motivo, puede_tecnico, accion, principal, al_asignar_tecnico)
SELECT origen.id_estado_ot, destino.id_estado_ot, v.exige_motivo, v.puede_tecnico, v.accion, v.principal, v.al_asignar_tecnico
FROM (VALUES
    ('creada', 'asignada', false, false, NULL, false, true),
    ('creada', 'cancelada', true, false, NULL, false, false),
    ('asignada', 'en_ejecucion', false, true, 'Iniciar', true, false),
    ('asignada', 'cancelada', true, false, NULL, false, false),
    ('en_ejecucion', 'pausada', true, true, 'Pausar', false, false),
    ('en_ejecucion', 'finalizada', false, true, 'Finalizar', true, false),
    ('pausada', 'en_ejecucion', false, true, 'Reanudar', true, false),
    ('pausada', 'cancelada', true, false, NULL, false, false),
    ('finalizada', 'cerrada', true, false, NULL, false, false),
    ('finalizada', 'asignada', true, false, NULL, false, false)
) AS v(origen, destino, exige_motivo, puede_tecnico, accion, principal, al_asignar_tecnico)
JOIN operativo.estado_ot AS origen ON origen.nombre = v.origen
JOIN operativo.estado_ot AS destino ON destino.nombre = v.destino
ON CONFLICT (id_estado_origen, id_estado_destino) DO NOTHING;
