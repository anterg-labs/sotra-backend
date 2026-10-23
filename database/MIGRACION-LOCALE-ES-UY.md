# Migración del locale de PostgreSQL a español de Uruguay

`compose.yml` inicializa los volúmenes nuevos con el proveedor ICU y `es-UY`. PostgreSQL solo toma `POSTGRES_INITDB_ARGS` cuando el directorio de datos está vacío. La base local existente fue creada con proveedor `libc` y `en_US.utf8`; cambiar Compose no modifica su locale. Los listados ya aplican `es-UY-x-icu` al ordenar texto, por lo que su resultado no depende del locale predeterminado de esa base.

Para cambiar también el locale predeterminado de una base con datos hay que crear otra base y restaurar una copia. No usar `docker compose down -v` ni borrar la base anterior. La API y Keycloak comparten `DB_NAME`; el cambio requiere detener sus escrituras, respaldar también el esquema `keycloak` y reiniciar ambos contra la nueva base. No se realiza automáticamente.

Procedimiento de migración, durante una ventana acordada:

1. Registrar nombre, tamaño, proveedor y locale de la base actual con `SELECT current_database(), datlocprovider, datcollate, datlocale FROM pg_database WHERE datname = current_database();`. Confirmar espacio suficiente para respaldo y nueva base. Detener las escrituras de la API y Keycloak sin detener PostgreSQL.
2. Crear un respaldo completo con `pg_dump --format=custom` fuera del repositorio y comprobar que terminó con código 0. Conservar la base anterior intacta para volver atrás. Las credenciales se obtienen del entorno; no se imprimen ni se incluyen en el respaldo versionado.
3. Crear una base nueva desde `template0` con `LOCALE_PROVIDER icu`, `ICU_LOCALE 'es-UY'` y locale auxiliar `C.utf8`, conservando el propietario de la base anterior. Restaurar con `pg_restore --exit-on-error`, sin `--no-owner`, para mantener propietarios y permisos de los esquemas `operativo`, `vectorial` y `keycloak`.
4. Comparar cantidades de tablas y registros relevantes, secuencias, extensiones, roles y permisos; comprobar que `datlocprovider = 'i'` y `datlocale = 'es-UY'`. Probar una consulta `ORDER BY` de texto, la API y el inicio, renovación y cierre de sesión de Keycloak.
5. Cambiar `DB_NAME` en el `.env` local a la nueva base y recrear los servicios que dependen de ella. Si alguna comprobación falla, volver al nombre anterior y recrear esos servicios. No eliminar la base anterior hasta completar la validación.

La migración del volumen local y de cualquier entorno remoto se planifica por separado. No cambia la versión de Keycloak ni de Java, pero sí exige interrumpir y volver a iniciar sus conexiones a la base compartida.
