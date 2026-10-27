# CHANGELOG

## Version 1.0.75 (2026-10-26)

- IA: se quita Ollama. Los embeddings `bge-m3` corren dentro de la API con ONNX (como el reranker) y el resumen de las sugerencias lo redacta un proveedor externo con API compatible con OpenAI, por defecto Groq con `openai/gpt-oss-20b` (`IA_API_URL`, `IA_API_MODELO`, `IA_API_CLAVE`, `IA_API_ESFUERZO`). Funciona en un servidor sin GPU.
- IA: si el proveedor no responde en 20 segundos, rechaza el pedido o falta la clave, la sugerencia se arma con el texto del manual sin resumir. La respuesta incluye `modo` (`ia`, `sin_ia` o `descartada`) para que el frontend avise cuando el asistente no está disponible; el modo queda en `historial_recomendaciones_ia`.
- IA: el `502 SERVICIO_IA_NO_DISPONIBLE` ahora indica que faltan los modelos locales; el proveedor externo ya no produce errores en las sugerencias.
- IA: las secciones de los manuales se reconocen también por sus títulos numerados ("4.2 Cambio del filtro"), y un título al final de un fragmento ya no recibe como cuerpo el texto de la sección anterior.
- IA: las instrucciones del resumen piden conservar las obligaciones de seguridad como obligaciones.
- IA: la distancia máxima de la búsqueda ya no descarta los fragmentos que contienen las palabras de la consulta; el reranker decide si responden. Antes, procedimientos como "5.1 Engrase del brazo" quedaban sin encontrar porque su fragmento mezclaba varios temas.
- Base de datos: columna `modo` en `historial_recomendaciones_ia`, en el SQL inicial y en `database/cambios/2026-10-24-historial-ia-modo.sql` para bases existentes.
- Los documentos técnicos cargados antes de esta versión deben volver a subirse para usar los nuevos embeddings y el nuevo divisor de secciones.
- Docker: `compose.yml` descarga los modelos de IA locales (servicio `modelos-ia`, revisiones fijas de Hugging Face) y el backend ya no depende de una carpeta del equipo (`RERANKER_CARPETA`). Se elimina `compose.ia.yml`.
- Docker: se restaura `Dockerfile.dev`, que `compose.dev.yml` necesita para la recarga en caliente.
- Herramienta `ComparadorIa` para comparar proveedores de IA con el corpus de evaluación.
- IA: la búsqueda de sugerencias usa solo la documentación del activo de la orden, de su modelo y la general (`activoId` en `POST /ordenes-trabajo/sugerencia`); ya no cita manuales de otras máquinas.
- IA: al procesar un PDF se quitan los encabezados y pies que se repiten en las páginas, para que una sección no se corte en el cambio de página. Los títulos en mayúsculas y los de procedimiento se reconocen, y los pasos numerados ya no se toman como títulos.
- IA: el proveedor redacta un título en español para la sugerencia (el título original queda en "Fuente") y responde `SIN_INFORMACION` si la sección no trata la tarea; en ese caso la API devuelve `422`. Las páginas de índice ya no se usan como respuesta.
- IA: búsqueda entre idiomas: con manuales en inglés, una sección con puntaje del reranker entre −2 y 0 se acepta si su distancia coseno a la consulta es de 0,40 o menos. Reglas de vocabulario técnico para la traducción.
- IA: límite de llamadas al proveedor para toda la API (`IA_API_LLAMADAS_POR_MINUTO`, 10 por defecto). La que se pasa usa el texto del manual con aviso (`modo = sin_ia`).
- Documentos técnicos: `POST /documentos-tecnicos/{id}/reprocesar` vuelve a procesar el PDF guardado con el divisor actual (`pdf-v5`), sin volver a subirlo.
- Base de datos: se elimina `orden_trabajo.id_recomendacion_ia`, que nada escribía (`database/cambios/2026-10-25-orden-sin-id-recomendacion-ia.sql`; los vínculos que hubiera pasan al historial).
- Datos: cliente SUPER CONSTRUCTURA SLA con 6 activos SANY y los modelos SY335, SY235 y SY155H (`database/cambios/2026-10-25-cliente-super-constructura-activos-sany.sql`).

## Version 1.0.74 (2026-10-24)

- Usuarios: un campo vacío (nombre, apellido o teléfono) ya no repite el mensaje «es obligatorio» dos veces en la respuesta de error del alta y la modificación.

## Version 1.0.73 (2026-10-24)

- Documentación: el README advierte al inicio que el despliegue a producción no aplica los SQL de `database/cambios/` (local y staging sí) y deja la decisión pendiente.

## Version 1.0.72 (2026-10-23)

- Clientes: el teléfono conserva el cero inicial del celular al guardar y mostrar; el alta y la edición impiden duplicados con y sin cero.
- Base de datos: se agrega el índice único sobre el teléfono comparable de clientes, en el SQL inicial y en el cambio para bases existentes, que se aplica solo en el despliegue.

## Version 1.0.71 (2026-10-23)

- Usuarios: la modificación de un usuario ya no permite dejarlo sin roles; igual que el alta, responde `400` si `idsRol` viene vacío o ausente.

## Version 1.0.70 (2026-10-23)

- Roles: el listado de roles (`GET /api/v1/roles`) ahora informa el estado de cada rol (`activo` o `inactivo`).

## Version 1.0.69 (2026-10-23)

- Usuarios: se conserva el cero inicial del celular al guardar y mostrar el teléfono; el alta y la edición impiden duplicados con y sin cero.
- Base de datos: se agrega un índice único sobre el teléfono comparable, tanto al SQL inicial como al cambio para bases existentes, que se aplica solo en el despliegue.

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
