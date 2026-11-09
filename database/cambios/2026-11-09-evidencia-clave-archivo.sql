-- Los archivos de las evidencias pasan a Cloudflare R2 (bucket sotra, carpeta evidencias/). La fila guarda la clave
-- del objeto en clave_archivo; la columna archivo queda solo para las evidencias anteriores, que la API mueve a R2 al
-- arrancar con R2 configurado (MigradorDeEvidenciasAlAlmacen). Idempotente; lo aplica el despliegue (cargar-datos.sh).

BEGIN;

ALTER TABLE operativo.evidencia_ot
    ADD COLUMN IF NOT EXISTS clave_archivo character varying(300);

COMMIT;
