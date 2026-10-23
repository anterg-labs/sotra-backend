-- El stock y el mínimo de un repuesto nunca pueden ser negativos.
-- Idempotente. Si hay filas que lo incumplan falla y no cambia nada; revisarlas con:
--   SELECT id_repuesto, codigo, cantidad, cant_min FROM operativo.repuesto WHERE cantidad < 0 OR cant_min < 0;
ALTER TABLE operativo.repuesto DROP CONSTRAINT IF EXISTS "CK_REPUESTO_CANTIDAD_NO_NEGATIVA";
ALTER TABLE operativo.repuesto DROP CONSTRAINT IF EXISTS "CK_REPUESTO_CANT_MIN_NO_NEGATIVA";
ALTER TABLE operativo.repuesto
    ADD CONSTRAINT "CK_REPUESTO_CANTIDAD_NO_NEGATIVA" CHECK (cantidad >= 0),
    ADD CONSTRAINT "CK_REPUESTO_CANT_MIN_NO_NEGATIVA" CHECK (cant_min IS NULL OR cant_min >= 0);
