#!/bin/sh
set -e

carpeta=${DATOS_DIR:-/datos}
carpeta_cambios=${CAMBIOS_DIR:-/cambios}
archivo_operativo=$carpeta/01-init.sql
archivo_vectorial=$carpeta/02-vectorial.sql

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
psql -v ON_ERROR_STOP=1 -c "CREATE TABLE IF NOT EXISTS public.cambios_aplicados (archivo text PRIMARY KEY, aplicado_en timestamptz NOT NULL DEFAULT now())"

registrar_cambio() {
  echo "INSERT INTO public.cambios_aplicados (archivo) VALUES (:'archivo') ON CONFLICT (archivo) DO NOTHING" | psql -v ON_ERROR_STOP=1 -v archivo="$1"
}

tablas_operativas=$(psql -tA -c "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'operativo'")

if [ "$tablas_operativas" = "0" ] || [ "$RECREAR_BASE" = "1" ]; then
  if [ "$tablas_operativas" != "0" ]; then
    echo "RECREAR_BASE=1: se borra y recrea el esquema operativo con 01-init.sql. Los datos actuales se pierden."
  else
    echo "Base nueva: se crea el esquema operativo con 01-init.sql."
  fi

  huella_nueva=$(sha256sum "$archivo_operativo" | cut -d " " -f 1)
  echo "INSERT INTO public.carga_datos (archivo, huella) VALUES ('01-init.sql', '$huella_nueva') ON CONFLICT (archivo) DO UPDATE SET huella = EXCLUDED.huella, cargado_en = now();" > /tmp/registrar-huella.sql
  psql -v ON_ERROR_STOP=1 --single-transaction -f "$archivo_operativo" -f /tmp/registrar-huella.sql

  # 01-init.sql ya incluye todos los cambios incrementales existentes.
  for cambio in "$carpeta_cambios"/*.sql; do
    [ -e "$cambio" ] || continue
    registrar_cambio "$(basename "$cambio")"
  done
else
  echo "El esquema operativo ya existe: no se recrea. Se aplican los cambios pendientes de $carpeta_cambios."
  if [ ! -d "$carpeta_cambios" ]; then
    echo "AVISO: no existe $carpeta_cambios; no se aplica ningun cambio. Montar database/cambios en el contenedor."
  fi
  aplicados=0
  for cambio in "$carpeta_cambios"/*.sql; do
    [ -e "$cambio" ] || continue
    nombre=$(basename "$cambio")
    ya_aplicado=$(echo "SELECT count(*) FROM public.cambios_aplicados WHERE archivo = :'archivo'" | psql -tA -v ON_ERROR_STOP=1 -v archivo="$nombre")
    if [ "$ya_aplicado" = "0" ]; then
      echo "Aplicando $nombre"
      psql -v ON_ERROR_STOP=1 -f "$cambio"
      registrar_cambio "$nombre"
      aplicados=$((aplicados + 1))
    fi
  done
  echo "Cambios aplicados: $aplicados."
fi

existe_vectorial=$(psql -tA -c "SELECT count(*) FROM information_schema.schemata WHERE schema_name = 'vectorial'")

if [ "$existe_vectorial" = "0" ]; then
  echo "El esquema vectorial no existe: se crea la estructura vacia con 02-vectorial.sql."
  psql -v ON_ERROR_STOP=1 --single-transaction -f "$archivo_vectorial"
else
  echo "El esquema vectorial ya existe: no se modifica."
fi

echo "Carga de datos terminada."
