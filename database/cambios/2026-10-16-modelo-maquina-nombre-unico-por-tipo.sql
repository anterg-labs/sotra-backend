-- Evita modelos de máquina duplicados: mismo nombre (sin distinguir mayúsculas) dentro del mismo tipo.
-- Se aplica a mano. Antes de ejecutarlo, revisar si ya hay duplicados:
--   SELECT lower(nombre), id_tipo_maquina, count(*) FROM operativo.modelo_maquina GROUP BY 1, 2 HAVING count(*) > 1;
CREATE UNIQUE INDEX IF NOT EXISTS "UX_MODELO_MAQUINA_NOMBRE_TIPO"
    ON operativo.modelo_maquina (lower(nombre), id_tipo_maquina);
