-- La contraseña de los usuarios vive solo en Keycloak: se elimina la columna del
-- login propio. Se puede ejecutar más de una vez sin error.

ALTER TABLE operativo.usuario DROP COLUMN IF EXISTS contrasena;
