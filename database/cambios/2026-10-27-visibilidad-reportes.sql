-- Preferencias globales de visualización; la ausencia de una fila equivale a visible.
BEGIN;
CREATE TABLE IF NOT EXISTS operativo.visibilidad_reporte (
    codigo character varying(100) PRIMARY KEY,
    visible boolean NOT NULL,
    CONSTRAINT "CK_VISIBILIDAD_REPORTE_CODIGO" CHECK (codigo ~ '^[a-z][a-z0-9_]{1,99}$')
);
COMMIT;
