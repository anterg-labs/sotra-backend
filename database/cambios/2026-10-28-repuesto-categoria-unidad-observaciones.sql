-- Datos del repuesto que pide RF004-01/03/05/06: categoría o tipo, modelo o compatibilidad, unidad de medida y
-- observaciones, además de un nombre que no se repite. Idempotente; lo aplica el despliegue (cargar-datos.sh).
-- Si la base ya tiene dos repuestos con el mismo nombre, el cambio se detiene con un mensaje que lo explica en lugar de
-- renombrar datos en silencio: hay que corregir esos nombres a mano y volver a desplegar.

BEGIN;

ALTER TABLE operativo.repuesto
    ADD COLUMN IF NOT EXISTS categoria character varying(80),
    ADD COLUMN IF NOT EXISTS modelo_compatibilidad character varying(120),
    ADD COLUMN IF NOT EXISTS unidad_medida character varying(30) NOT NULL DEFAULT 'unidad',
    ADD COLUMN IF NOT EXISTS observaciones text;

CREATE INDEX IF NOT EXISTS "IX_REPUESTO_CATEGORIA" ON operativo.repuesto (lower(categoria)) WHERE categoria IS NOT NULL;

DO $repuesto_nombre_unico$
DECLARE
    repetidos text;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'operativo' AND indexname = 'UK_REPUESTO_NOMBRE') THEN
        SELECT string_agg(nombre_repetido, ', ') INTO repetidos
        FROM (SELECT min(nombre) AS nombre_repetido
              FROM operativo.repuesto
              GROUP BY lower(btrim(nombre))
              HAVING count(*) > 1) AS r;

        IF repetidos IS NOT NULL THEN
            RAISE EXCEPTION 'No se puede exigir nombre único de repuesto: hay nombres repetidos (%). Corrija esos repuestos y vuelva a aplicar el cambio.', repetidos;
        END IF;

        CREATE UNIQUE INDEX "UK_REPUESTO_NOMBRE" ON operativo.repuesto (lower(btrim(nombre)));
    END IF;
END
$repuesto_nombre_unico$;

COMMIT;
