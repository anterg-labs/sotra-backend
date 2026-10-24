# CHANGELOG

## Version 1.0.71 (2026-10-23)

- Usuarios: la modificación de un usuario ya no permite dejarlo sin roles; igual que el alta, responde `400` si `idsRol` viene vacío o ausente.

## Version 1.0.68 (2026-10-23)

- Listados: el texto se ordena con intercalación española sin distinguir mayúsculas, antes de paginar; documentos técnicos aplica el mismo criterio.
- Base de datos: los volúmenes PostgreSQL nuevos se inicializan con ICU `es-UY`; se documenta la migración sin pérdida de datos de las bases existentes.

## Version 1.0.67 (2026-10-23)

- Integración: se reúnen las correcciones de usuarios, incluida su reactivación, los contratos de actualización y los errores uniformes con los cambios de listados, stock, documentos, reportes y analítica.

## Version 1.0.66 (2026-10-23)

- Analítica: las consultas repetidas con los mismos filtros reutilizan su resultado durante un plazo configurable y comparten el cálculo cuando llegan a la vez.

## Version 1.0.65 (2026-10-23)

- Acceso: el formulario de Keycloak informa los campos obligatorios, muestra un estado al ingresar y evita envíos duplicados.

## Version 1.0.64 (2026-10-23)

- Base de datos: la carga inicial conserva los datos existentes y aplica los cambios SQL pendientes una sola vez. Se agregan los servicios opcionales de IA al entorno de desarrollo.

## Version 1.0.63 (2026-10-23)

- IA: las consultas a Ollama reintentan fallos transitorios y responden más rápido cuando el servicio permanece caído.

## Version 1.0.62 (2026-10-23)

- Reportes: los PDF muestran la identidad visual de CVM y avisan cuando el listado de órdenes supera el máximo exportable.
- Monitoreo: se registran métricas de órdenes por estado, tiempo de cierre y niveles de stock.

## Version 1.0.61 (2026-10-23)

- Documentación técnica: se pueden registrar y modificar título, tipo, descripción y asociación, filtrar documentos y cambiar su vigencia. Los obsoletos dejan de alimentar las sugerencias.

## Version 1.0.60 (2026-10-23)

- Stock: el ingreso requiere solo la cantidad; la fecha y la ubicación se toman del sistema y del repuesto. Los movimientos dejan de guardar un estado sin uso.

## Version 1.0.59 (2026-10-23)

- Órdenes de trabajo: se evita duplicar una asignación al mismo técnico y se informa el conflicto.
- Listados: usuarios y clientes muestran la cantidad de órdenes activas asociadas y permiten ordenar por ese valor.

## Version 1.0.58 (2026-10-23)

- Listados: se pueden ordenar por columnas permitidas en los listados de usuarios, clientes, máquinas, órdenes, repuestos y movimientos, con paginación estable.

## Version 1.0.57 (2026-10-23)

- Pruebas: se adaptan las comprobaciones de actualización a los contratos completos de activos, clientes, tipos, modelos y repuestos.

## Version 1.0.56 (2026-10-23)

- Contratos: las actualizaciones distinguen campos opcionales vacíos o nulos de campos faltantes y rechazan valores obligatorios ausentes.
- Pruebas: se actualizan las comprobaciones de usuarios para el rol obligatorio y el conteo de órdenes del listado.

## Version 1.0.55 (2026-10-23)

- Pruebas: se dejan las comprobaciones de reactivación de usuarios para su rama correspondiente, junto con esa funcionalidad.

## Version 1.0.54 (2026-10-23)

- Usuarios: se acepta el celular con cero inicial y se guarda normalizado; el alta informa si Keycloak envió el correo y el reenvío avisa cuando falla.

## Version 1.0.53 (2026-10-23)

- Integración: se conserva el formato de validación de la API tras registrar los controladores y las variables del entorno tienen prioridad sobre el archivo local de configuración.
- Pruebas: se corrige la ubicación del corpus de evaluación y se hace explícita la selección del mapeador en el ejecutor de regresión después de integrar Keycloak.
