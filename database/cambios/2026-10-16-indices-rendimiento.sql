-- Índices para claves foráneas y filtros frecuentes. Con 30 000 órdenes, filtrar por estado, el dashboard y las alertas
-- tardaban de 10 s a más de 60 s por el recorrido completo de historial_estado_ot.
-- Aplicar a mano; es idempotente (IF NOT EXISTS). En producción conviene ejecutarlo fuera del horario pico.
CREATE INDEX IF NOT EXISTS "IX_HISTORIAL_ESTADO_OT_ORDEN_FECHA" ON operativo.historial_estado_ot (id_orden, fecha_hora DESC, id_h_estado_ot DESC);
CREATE INDEX IF NOT EXISTS "IX_HISTORIAL_ESTADO_OT_ESTADO" ON operativo.historial_estado_ot (id_estado_ot);
CREATE INDEX IF NOT EXISTS "IX_ORDEN_TRABAJO_ID_ACTIVO" ON operativo.orden_trabajo (id_activo);
CREATE INDEX IF NOT EXISTS "IX_ORDEN_TRABAJO_ID_CLIENTE" ON operativo.orden_trabajo (id_cliente);
CREATE INDEX IF NOT EXISTS "IX_ORDEN_TRABAJO_FECHA_CREADA" ON operativo.orden_trabajo (fecha_creada DESC, id_orden DESC);
CREATE INDEX IF NOT EXISTS "IX_OT_REPUESTO_ID_ORDEN" ON operativo.ot_repuesto (id_orden);
CREATE INDEX IF NOT EXISTS "IX_OT_REPUESTO_ID_REPUESTO" ON operativo.ot_repuesto (id_repuesto);
CREATE INDEX IF NOT EXISTS "IX_ACTIVO_ID_CLIENTE" ON operativo.activo (id_cliente);
CREATE INDEX IF NOT EXISTS "IX_MOVIMIENTO_STOCK_FECHA" ON operativo.movimiento_stock (fecha DESC, id_movimiento DESC);
CREATE INDEX IF NOT EXISTS "IX_HISTORIAL_RECOMENDACIONES_IA_ID_ORDEN" ON operativo.historial_recomendaciones_ia (id_orden);
CREATE INDEX IF NOT EXISTS "IX_EVIDENCIA_OT_ID_ORDEN" ON operativo.evidencia_ot (id_orden);
ANALYZE;
