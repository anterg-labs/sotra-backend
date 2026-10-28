-- A18: umbrales en horas por etapa. NULL usa la media histórica de tiempos terminados.
-- Sin máximos de negocio; cero es válido. Se rechazan duraciones negativas, NaN e infinitos.
BEGIN;
CREATE TABLE IF NOT EXISTS operativo.umbral_etapa_ot (
    etapa character varying(20) NOT NULL,
    umbral_horas double precision,
    CONSTRAINT "PK_UMBRAL_ETAPA_OT" PRIMARY KEY (etapa),
    CONSTRAINT "CK_UMBRAL_ETAPA_OT_ETAPA" CHECK (etapa IN ('pendiente', 'en_progreso', 'revision', 'pausa')),
    CONSTRAINT "CK_UMBRAL_ETAPA_OT_HORAS" CHECK (umbral_horas IS NULL OR (umbral_horas >= 0 AND umbral_horas < 'Infinity'::double precision))
);
INSERT INTO operativo.umbral_etapa_ot (etapa, umbral_horas)
VALUES ('pendiente', NULL), ('en_progreso', NULL), ('revision', NULL), ('pausa', NULL)
ON CONFLICT (etapa) DO NOTHING;
COMMIT;
