-- Estado del procesamiento de cada documento técnico: la subida guarda el PDF y responde enseguida, y los fragmentos
-- y embeddings se generan en segundo plano («pendiente» → «procesando» → «listo» o «error»). Los documentos que ya
-- existen quedan «listo». Es aditivo e idempotente. Las bases nuevas lo traen en database/init/02-vectorial.sql.
-- Si el esquema vectorial todavía no existe, no hace nada: 02-vectorial.sql lo crea completo después.
DO $$
BEGIN
    IF to_regclass('vectorial.documento_tecnico') IS NULL THEN
        RETURN;
    END IF;

    ALTER TABLE vectorial.documento_tecnico
        ADD COLUMN IF NOT EXISTS estado_procesamiento character varying(20) DEFAULT 'listo' NOT NULL,
        ADD COLUMN IF NOT EXISTS error_procesamiento character varying(50);

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'CK_DOCUMENTO_TECNICO_ESTADO_PROCESAMIENTO'
          AND conrelid = 'vectorial.documento_tecnico'::regclass
    ) THEN
        ALTER TABLE vectorial.documento_tecnico
            ADD CONSTRAINT "CK_DOCUMENTO_TECNICO_ESTADO_PROCESAMIENTO"
            CHECK (estado_procesamiento IN ('pendiente', 'procesando', 'listo', 'error'));
    END IF;
END
$$;
