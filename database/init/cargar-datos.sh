#!/bin/sh
set -e

carpeta=/datos
archivo_operativo=$carpeta/01-init.sql
archivo_vectorial=$carpeta/02-vectorial.sql

huella_nueva=$(sha256sum "$archivo_operativo" | cut -d " " -f 1)

# Usuario y esquema de Keycloak dentro de la misma base. Se crean solo si faltan,
# antes de comparar huellas, para que Keycloak arranque aunque no haya diferencias.
if [ -n "$KC_DB_USERNAME" ]; then
  psql -v ON_ERROR_STOP=1 -v usuario="$KC_DB_USERNAME" -v contrasena="$KC_DB_PASSWORD" <<'SQL'
SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', :'usuario', :'contrasena')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'usuario') \gexec
SELECT format('CREATE SCHEMA IF NOT EXISTS keycloak AUTHORIZATION %I', :'usuario') \gexec
SELECT format('ALTER ROLE %I SET search_path = keycloak', :'usuario') \gexec
SQL
fi

psql -v ON_ERROR_STOP=1 -c "CREATE TABLE IF NOT EXISTS public.carga_datos (archivo text PRIMARY KEY, huella text NOT NULL, cargado_en timestamptz NOT NULL DEFAULT now())"

huella_actual=$(psql -tA -c "SELECT huella FROM public.carga_datos WHERE archivo = '01-init.sql'")

if [ "$huella_nueva" = "$huella_actual" ]; then
  echo "Sin diferencias: el esquema operativo ya tiene cargado 01-init.sql."
  exit 0
fi

echo "Se detectaron diferencias: se recrea el esquema operativo con 01-init.sql."

echo "INSERT INTO public.carga_datos (archivo, huella) VALUES ('01-init.sql', '$huella_nueva') ON CONFLICT (archivo) DO UPDATE SET huella = EXCLUDED.huella, cargado_en = now();" > /tmp/registrar-huella.sql

psql -v ON_ERROR_STOP=1 --single-transaction -f "$archivo_operativo" -f /tmp/registrar-huella.sql

existe_vectorial=$(psql -tA -c "SELECT count(*) FROM information_schema.schemata WHERE schema_name = 'vectorial'")

if [ "$existe_vectorial" = "0" ]; then
  echo "El esquema vectorial no existe: se crea la estructura vacia con 02-vectorial.sql."
  psql -v ON_ERROR_STOP=1 --single-transaction -f "$archivo_vectorial"
else
  echo "El esquema vectorial ya existe: no se modifica."
fi

echo "Carga de datos terminada."
