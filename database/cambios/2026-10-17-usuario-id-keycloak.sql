-- Vincula cada usuario de SOTRA con su usuario de Keycloak (el "sub" del token).
-- Es opcional: un usuario queda sin vincular hasta su primer ingreso, cuando la API
-- lo vincula por correo. Se puede ejecutar más de una vez sin error.

ALTER TABLE operativo.usuario ADD COLUMN IF NOT EXISTS id_keycloak uuid;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'UK_ID_KEYCLOAK_USUARIO') THEN
        ALTER TABLE operativo.usuario ADD CONSTRAINT "UK_ID_KEYCLOAK_USUARIO" UNIQUE (id_keycloak);
    END IF;
END
$$;
