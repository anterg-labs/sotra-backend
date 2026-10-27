-- Modelos SANY con manual técnico disponible y un cliente con un activo de cada uno.
-- Lo aplica solo el despliegue (database/init/cargar-datos.sh) y lo registra en public.cambios_aplicados.
-- Es idempotente: no duplica filas si ya existen el modelo, el cliente o el número de serie.
INSERT INTO operativo.modelo_maquina (nombre, id_tipo_maquina, descripcion, activo)
SELECT v.nombre, t.id_tipo_maquina, v.descripcion, true
FROM (VALUES
    ('SANY SY155H', 'Excavadora hidráulica de 15 toneladas.'),
    ('SANY SY235', 'Excavadora hidráulica de 23 toneladas.'),
    ('SANY SY335', 'Excavadora hidráulica de 33 toneladas.')
) AS v (nombre, descripcion)
JOIN operativo.tipo_maquina t ON t.nombre = 'Excavadora hidráulica'
WHERE NOT EXISTS (
    SELECT 1 FROM operativo.modelo_maquina m
    WHERE m.nombre = v.nombre AND m.id_tipo_maquina = t.id_tipo_maquina
);

INSERT INTO operativo.cliente (razon_social, nombre, observaciones, estado)
SELECT 'SUPER CONSTRUCTURA SLA', 'SUPER CONSTRUCTURA SLA',
    'Cliente con activos SANY que tienen manual técnico cargado.', true
WHERE NOT EXISTS (SELECT 1 FROM operativo.cliente WHERE razon_social = 'SUPER CONSTRUCTURA SLA');

INSERT INTO operativo.activo (id_cliente, num_serie, descripcion, id_modelo_maquina, anio_fabricacion, nombre, ubicacion, estado)
SELECT c.id_cliente, v.num_serie, v.descripcion, m.id_modelo_maquina, v.anio, v.nombre, v.ubicacion, true
FROM (VALUES
    ('SY135C-25-0301', 'Excavadora compacta para zanjeo urbano.', 'SANY SY135C', 2025, 'Excavadora SY135C #3', 'Obra Av. Italia, Montevideo'),
    ('SY155H-24-0118', 'Excavadora para movimiento de suelos.', 'SANY SY155H', 2024, 'Excavadora SY155H', 'Obra Av. Italia, Montevideo'),
    ('SY215C-24-0620', 'Excavadora para obra vial.', 'SANY SY215C', 2024, 'Excavadora SY215C #3', 'Obra Ruta 1 km 45'),
    ('SY235-23-0074', 'Excavadora para excavación de cimientos.', 'SANY SY235', 2023, 'Excavadora SY235', 'Obra Ruta 1 km 45'),
    ('SY335-22-0041', 'Excavadora de gran porte para cantera.', 'SANY SY335', 2022, 'Excavadora SY335', 'Cantera de Minas'),
    ('SY365H-23-0057', 'Excavadora de gran porte para cantera.', 'SANY SY365H', 2023, 'Excavadora SY365H #2', 'Cantera de Minas')
) AS v (num_serie, descripcion, modelo, anio, nombre, ubicacion)
JOIN operativo.modelo_maquina m ON m.nombre = v.modelo
JOIN operativo.cliente c ON c.razon_social = 'SUPER CONSTRUCTURA SLA'
WHERE NOT EXISTS (SELECT 1 FROM operativo.activo a WHERE a.num_serie = v.num_serie);
