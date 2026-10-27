-- Una orden puede tener varias sugerencias: el vínculo está en historial_recomendaciones_ia.id_orden, con el
-- resultado aceptada o descartada. La columna orden_trabajo.id_recomendacion_ia quedó del diseño anterior y nada
-- la escribe. Antes de quitarla se pasa al historial cualquier vínculo que solo estuviera en la orden.
-- Lo aplica solo el despliegue (database/init/cargar-datos.sh) y lo registra en public.cambios_aplicados.
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'operativo' AND table_name = 'orden_trabajo' AND column_name = 'id_recomendacion_ia'
    ) THEN
        UPDATE operativo.historial_recomendaciones_ia AS historial
        SET id_orden = orden.id_orden, resultado = 'aceptada'
        FROM operativo.orden_trabajo AS orden
        WHERE orden.id_recomendacion_ia = historial.id_recomendacion_ia
          AND historial.id_orden IS NULL;
    END IF;
END $$;

ALTER TABLE operativo.orden_trabajo DROP CONSTRAINT IF EXISTS "FK_RECOMENDACION_IA_ORDEN_TRABAJO";
ALTER TABLE operativo.orden_trabajo DROP COLUMN IF EXISTS id_recomendacion_ia;
