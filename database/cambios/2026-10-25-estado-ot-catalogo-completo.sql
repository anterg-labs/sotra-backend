-- Completa el catálogo de estados de las órdenes: agrega «cancelada» (el backend la creaba al vuelo) y las etiquetas
-- que muestran el filtro, los formularios y los reportes. Idempotente; lo aplica el despliegue (cargar-datos.sh).
INSERT INTO operativo.estado_ot (nombre, descripcion, estado)
SELECT 'cancelada', 'Cancelada', true
WHERE NOT EXISTS (SELECT 1 FROM operativo.estado_ot WHERE nombre = 'cancelada');

UPDATE operativo.estado_ot AS e
SET descripcion = v.etiqueta
FROM (VALUES
    ('creada', 'Creada'),
    ('asignada', 'Asignada'),
    ('en_ejecucion', 'En ejecución'),
    ('pausada', 'Pausada'),
    ('finalizada', 'Finalizada'),
    ('cerrada', 'Cerrada'),
    ('cancelada', 'Cancelada')
) AS v(nombre, etiqueta)
WHERE e.nombre = v.nombre AND (e.descripcion IS NULL OR btrim(e.descripcion) = '');
