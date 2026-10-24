-- Conserva el cero inicial mostrado al usuario, sin permitir duplicados con y sin ese cero.
-- Lo aplica solo el despliegue (database/init/cargar-datos.sh) y lo registra en public.cambios_aplicados.
-- Si hay teléfonos duplicados con y sin cero, falla a propósito y el backend no arranca hasta resolverlos.
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
