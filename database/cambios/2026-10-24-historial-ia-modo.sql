-- Modo de redacción de cada sugerencia de IA: «ia» (resumen del proveedor), «sin_ia» (el proveedor no respondió y se
-- mostró el texto del manual con aviso) o «descartada» (el control de números descartó el resumen).
-- Es aditivo e idempotente. Aplicar a mano a las bases existentes; las bases nuevas lo traen en database/init/01-init.sql.
-- Las sugerencias anteriores quedan como «ia».

ALTER TABLE operativo.historial_recomendaciones_ia
    ADD COLUMN IF NOT EXISTS modo character varying(20) NOT NULL DEFAULT 'ia';

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_HISTORIAL_RECOMENDACIONES_IA_MODO') THEN
        ALTER TABLE operativo.historial_recomendaciones_ia
            ADD CONSTRAINT "CK_HISTORIAL_RECOMENDACIONES_IA_MODO"
            CHECK (modo IN ('ia', 'sin_ia', 'descartada'));
    END IF;
END $$;
