-- Cabecera de contexto de los fragmentos (documento, máquina y sección): se guarda aparte del texto y entra en la
-- búsqueda por palabras. Idempotente; lo aplica el despliegue (cargar-datos.sh). Los fragmentos existentes quedan con
-- la cabecera vacía hasta que se reprocesa su documento (POST /api/v1/documentos-tecnicos/{id}/reprocesar).

BEGIN;

ALTER TABLE vectorial.fragmentos
    ADD COLUMN IF NOT EXISTS contexto text NOT NULL DEFAULT '';

DO $fragmentos_contexto$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'vectorial' AND table_name = 'fragmentos' AND column_name = 'texto_busqueda'
          AND generation_expression LIKE '%contexto%') THEN
        DROP INDEX IF EXISTS vectorial.fragmentos_texto_idx;
        ALTER TABLE vectorial.fragmentos DROP COLUMN IF EXISTS texto_busqueda;
        ALTER TABLE vectorial.fragmentos
            ADD COLUMN texto_busqueda tsvector
            GENERATED ALWAYS AS (to_tsvector('spanish'::regconfig, ((contexto || ' '::text) || texto))) STORED;
        CREATE INDEX fragmentos_texto_idx ON vectorial.fragmentos USING gin (texto_busqueda);
    END IF;
END
$fragmentos_contexto$;

COMMIT;
