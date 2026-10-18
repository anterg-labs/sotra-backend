-- Aplicación manual: elimina el historial antiguo, conservando el documento vigente y sus aceptaciones.
-- No agrega tablas. El campo version queda como marcador interno para detectar cambios del texto.
-- Transacción idempotente. Revisar/resguardar los datos antes de ejecutarla sobre una base existente.
BEGIN;
SELECT pg_advisory_xact_lock(7201017);
LOCK TABLE operativo.terminos_condiciones, operativo.aceptacion_terminos IN ACCESS EXCLUSIVE MODE;

DELETE FROM operativo.aceptacion_terminos
WHERE id_terminos IN (SELECT id_terminos FROM operativo.terminos_condiciones WHERE NOT vigente);
DELETE FROM operativo.terminos_condiciones WHERE NOT vigente;

ALTER TABLE operativo.terminos_condiciones ALTER COLUMN vigente SET DEFAULT true;
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                   WHERE conrelid = 'operativo.terminos_condiciones'::regclass
                     AND conname = 'CK_TERMINOS_SIEMPRE_VIGENTE') THEN
        ALTER TABLE operativo.terminos_condiciones
            ADD CONSTRAINT "CK_TERMINOS_SIEMPRE_VIGENTE" CHECK (vigente);
    END IF;
END $$;
COMMIT;
