-- Los PDF de los documentos técnicos pasan a Cloudflare R2 (bucket sotra, carpeta documentacion/). La fila guarda la
-- clave del objeto en clave_archivo y la columna archivo queda vacía; solo la usan los documentos anteriores, que la
-- API mueve a R2 al arrancar con R2 configurado (MigradorDeArchivosAlAlmacen). Los fragmentos y embeddings siguen en
-- vectorial.fragmentos. Idempotente; lo aplica el despliegue (cargar-datos.sh).

BEGIN;

ALTER TABLE vectorial.documento_tecnico
    ADD COLUMN IF NOT EXISTS clave_archivo character varying(300);

ALTER TABLE vectorial.documento_tecnico
    ALTER COLUMN archivo DROP NOT NULL;

COMMIT;
