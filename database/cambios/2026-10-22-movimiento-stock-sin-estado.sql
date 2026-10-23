-- Elimina la columna sin uso movimiento_stock.estado (siempre valía true).
-- Idempotente. NO se aplica automáticamente: ejecutar a mano en cada base existente.
-- Las bases nuevas ya nacen sin la columna (database/init/01-init.sql).
ALTER TABLE operativo.movimiento_stock DROP COLUMN IF EXISTS estado;
