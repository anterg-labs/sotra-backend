-- Conserva el cero inicial mostrado al usuario, sin permitir duplicados con y sin ese cero.
-- Aplicar a una base existente solo tras revisar y respaldar sus datos.
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM operativo.usuario
        GROUP BY CASE WHEN length(telefono) = 9 AND left(telefono, 1) = '0'
            THEN substring(telefono FROM 2) ELSE telefono END
        HAVING count(*) > 1
    ) THEN
        RAISE EXCEPTION 'Hay teléfonos de usuarios duplicados con y sin cero inicial; resolverlos antes de crear el índice';
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS "UK_TELEFONO_USUARIO_NORMALIZADO" ON operativo.usuario
    ((CASE WHEN length(telefono) = 9 AND left(telefono, 1) = '0'
        THEN substring(telefono FROM 2) ELSE telefono END));
