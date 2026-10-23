# CHANGELOG

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

## Version 1.0.53 (2026-10-23)

- Integración: se conserva el formato de validación de la API tras registrar los controladores y las variables del entorno tienen prioridad sobre el archivo local de configuración.
- Pruebas: se corrige la ubicación del corpus de evaluación y se hace explícita la selección del mapeador en el ejecutor de regresión después de integrar Keycloak.
