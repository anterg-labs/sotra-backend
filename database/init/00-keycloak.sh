#!/usr/bin/env bash
# Crea el usuario y el esquema que usa Keycloak dentro de la base de SOTRA.
# PostgreSQL lo ejecuta una sola vez, cuando el volumen de datos está vacío,
# antes de 01-init.sql. KC_DB_USERNAME y KC_DB_PASSWORD llegan desde compose.yml.

set -euo pipefail

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
    -v usuario="$KC_DB_USERNAME" -v contrasena="$KC_DB_PASSWORD" <<'SQL'
SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', :'usuario', :'contrasena') \gexec
SELECT format('CREATE SCHEMA keycloak AUTHORIZATION %I', :'usuario') \gexec
SELECT format('ALTER ROLE %I SET search_path = keycloak', :'usuario') \gexec
SQL
