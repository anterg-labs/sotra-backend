-- Datos generales de la documentación (RF006-01): título, tipo, descripción, vigencia y modelo de máquina.
-- Es aditivo e idempotente. Aplicar a mano a las bases existentes; las bases nuevas lo traen en database/init/02-vectorial.sql.
-- Los documentos existentes quedan como «manual_tecnico», vigentes y con el nombre del archivo como título.

ALTER TABLE vectorial.documento_tecnico ADD COLUMN IF NOT EXISTS titulo character varying(200);
ALTER TABLE vectorial.documento_tecnico ADD COLUMN IF NOT EXISTS tipo character varying(30) NOT NULL DEFAULT 'manual_tecnico';
ALTER TABLE vectorial.documento_tecnico ADD COLUMN IF NOT EXISTS descripcion text;
ALTER TABLE vectorial.documento_tecnico ADD COLUMN IF NOT EXISTS vigente boolean NOT NULL DEFAULT true;
ALTER TABLE vectorial.documento_tecnico ADD COLUMN IF NOT EXISTS modelo_maquina_id bigint;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_DOCUMENTO_TECNICO_TIPO') THEN
        ALTER TABLE vectorial.documento_tecnico
            ADD CONSTRAINT "CK_DOCUMENTO_TECNICO_TIPO"
            CHECK (tipo IN ('normativa', 'procedimiento_interno', 'manual_tecnico', 'politica_seguridad', 'otro'));
    END IF;
END $$;
