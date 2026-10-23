-- Ejecutar manualmente, previa revisión de datos. No elimina ni corrige registros.
-- Si hay textos obligatorios vacíos, VALIDATE falla y revierte todo este bloque.
-- Los NULL ya están impedidos por los NOT NULL existentes.
DO $validacion$
DECLARE
    regla record;
BEGIN
    FOR regla IN
        SELECT * FROM (VALUES
            ('activo', 'CK_ACTIVO_TEXTO_OBLIGATORIO', 'num_serie ~ ''[^[:space:]]'''),
            ('cliente', 'CK_CLIENTE_TEXTOS_OBLIGATORIOS', 'razon_social ~ ''[^[:space:]]'' AND nombre ~ ''[^[:space:]]'''),
            ('modelo_maquina', 'CK_MODELO_NOMBRE_OBLIGATORIO', 'nombre ~ ''[^[:space:]]'''),
            ('tipo_maquina', 'CK_TIPO_NOMBRE_OBLIGATORIO', 'nombre ~ ''[^[:space:]]'''),
            ('repuesto', 'CK_REPUESTO_TEXTOS_OBLIGATORIOS', 'codigo ~ ''[^[:space:]]'' AND nombre ~ ''[^[:space:]]'''),
            ('orden_trabajo', 'CK_ORDEN_DESCRIPCION_OBLIGATORIA', 'descripcion ~ ''[^[:space:]]'''),
            ('usuario', 'CK_USUARIO_TEXTOS_OBLIGATORIOS', 'nombre ~ ''[^[:space:]]'' AND apellido ~ ''[^[:space:]]'' AND mail ~ ''[^[:space:]]'' AND telefono ~ ''[^[:space:]]''')
        ) AS reglas(tabla, nombre, expresion)
    LOOP
        IF NOT EXISTS (
            SELECT 1 FROM pg_constraint
            WHERE conrelid = format('operativo.%I', regla.tabla)::regclass
              AND conname = regla.nombre
        ) THEN
            EXECUTE format('ALTER TABLE operativo.%I ADD CONSTRAINT %I CHECK (%s) NOT VALID',
                regla.tabla, regla.nombre, regla.expresion);
        END IF;
        EXECUTE format('ALTER TABLE operativo.%I VALIDATE CONSTRAINT %I', regla.tabla, regla.nombre);
    END LOOP;
END
$validacion$;
