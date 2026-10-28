# CHANGELOG

## Version 1.0.106 (2026-10-28)

- Documentación: se corrigen las versiones del frontend citadas en los apartados de mantenimiento planificado y demoras por etapa.

## Version 1.0.105 (2026-10-27)

- Desarrollo por red: emisor público de Keycloak configurable en Compose y herramienta local para registrar la IP detectada como callback, con respaldo y sin modificar usuarios o contraseñas.

## Version 1.0.104 (2026-10-27)

- Inicio: el indicador `ordenesSinAsignar` cuenta órdenes en fase pendiente sin técnico. El filtro `sinAsignar=true` en listado y calendario usa el mismo criterio; no cambia el esquema de la base.

## Version 1.0.103 (2026-10-27)

- Reportes: errores de API y fallos de sesión registrados en PostgreSQL con respaldo temporal y retención de 90 días; migraciones automáticas e inicialización de bases nuevas.

## Version 1.0.102 (2026-10-27)

- Reportes: consumo de repuestos en unidades descontadas al cerrar órdenes, filtrado por fecha del egreso, cliente y activo. Configuración persistente por elemento de Reportes; inicialización de bases nuevas y migración automática de la tabla de preferencias.

## Version 1.0.101 (2026-10-27)

- Reportes: se retira el endpoint de reparaciones atípicas y el ajuste Weibull por modelo. El reporte de intervalos entre correctivas conserva sus datos descriptivos; no hay cambios en la base.

## Version 1.0.100 (2026-10-27)

- Reportes: nuevo GET /api/v1/reportes/analisis/desgaste-edad-uso. Agrupa una observación por activo con órdenes en el período, correctivas, edad al último registro y última lectura conocida hasta esa fecha; conserva horas y kilómetros separados. Sin cambio de esquema.

## Version 1.0.99 (2026-10-27)

- Reportes A13: GET /api/v1/reportes/resumen-por-cliente agrupa órdenes, correctivas, correctivas cerradas, horas de parada y reincidencias por empresa cliente. Usa el criterio y la ventana configurable de reincidencias existentes, filtros y caché de analítica; sin cambios de base de datos.

## Version 1.0.98 (2026-10-27)

- Reportes A12: GET /api/v1/reportes/repuestos-por-modelo agrupa por modelo los repuestos vinculados a correctivas y cuenta órdenes asociadas y con suministro. Excluye vínculos liberados, admite filtros de fecha, cliente y activo y no modifica la base de datos.

## Version 1.0.97 (2026-10-27)

- A9: GET /api/v1/reportes/estacionalidad agrega correctivas por mes UTC de creación y tipo de máquina, con filtros, doce meses, ceros, totales y caché. Consulta agregada en PostgreSQL; sin cambios de esquema.

## Version 1.0.96 (2026-10-27)

- Reportes A18: tiempos acumulados por orden y etapa, media, mediana, P90, referencia histórica y listado paginado de excesos. El administrador configura o restablece umbrales sin máximos de negocio. Tabla `umbral_etapa_ot`, incremental idempotente e init actualizado; dump regenerado con datos ficticios en base descartable.

## Version 1.0.95 (2026-10-27)

- Reportes A17: porcentaje de mantenimiento planificado en horas en la comparativa de períodos y bloque `planificacion` en desempeño, con totales, meses y tipos de máquina. Solo órdenes con cierre; duración completa desde creación, incluidas pausas, sin datos de personas. No requiere cambios de base de datos.

## Version 1.0.94 (2026-10-27)

- Reportes: nuevos análisis de maquinaria y proceso: comparación del período con el anterior y con el año pasado, órdenes abiertas por antigüedad con el backlog en semanas, cumplimiento preventivo por tipo de orden y de máquina, horas en pausa por motivo, detalle por modelo, tiempo medio de reparación, Pareto de la parada, costo de la parada y estimado frente a real, órdenes atípicas y patrones de desgaste. Las tendencias suman las órdenes cerradas. No analizan personas.
- Avisos: nuevos avisos de ineficiencia por reglas (máquina que reincide, trabajo correctivo alto, preventivos incumplidos, órdenes envejecidas o sin movimiento, revisión estancada, reparaciones más lentas, parada concentrada y orden crítica sin asignar) con umbrales que el administrador puede cambiar sin límites fijos.
- Órdenes: tiempo estimado, causa de falla (catálogo nuevo), lectura de uso en horas o kilómetros con registro de cada cambio y motivo codificado de las pausas; la fecha prevista pasa a ser una columna propia; nuevo calendario de órdenes por fecha prevista y catálogo de motivos.
- Catálogos: los activos indican su unidad de uso, los tipos de máquina su costo por hora de parada y los repuestos su costo unitario.
- Base de datos: cambio incremental `2026-10-27-datos-para-reportes.sql` (también en `01-init.sql`) y `sotra-dump.sql` actualizado.

## Version 1.0.93 (2026-10-27)

- Documentos técnicos: la subida de un documento para las sugerencias de IA guarda el PDF y responde enseguida; los fragmentos y embeddings se generan en segundo plano, de a un documento por vez. Antes, un manual grande tardaba minutos dentro del pedido y Cloudflare lo cortaba a los 100 segundos (`524`), sin guardar nada.
- Documentos técnicos: cada documento informa `estadoProcesamiento` (`pendiente`, `procesando`, `listo` o `error`) y, si falló, `codigoErrorProcesamiento` y `mensajeErrorProcesamiento`. Al arrancar, la API retoma los que quedaron pendientes o a medio procesar.
- Documentos técnicos: `POST /documentos-tecnicos/{id}/reprocesar` responde `202` y pone el documento en la cola; mientras tanto, las sugerencias siguen usando los fragmentos anteriores.
- Base de datos: columnas `estado_procesamiento` y `error_procesamiento` en `vectorial.documento_tecnico`, en `02-vectorial.sql` y en `database/cambios/2026-10-27-documento-tecnico-estado-procesamiento.sql` para bases existentes.

## Version 1.0.92 (2026-10-27)

- Documentación de la API: el documento OpenAPI describe todas las propiedades de los DTO, documenta `ETag`, `If-Match` y el `412` de las órdenes, incluye ejemplos de los flujos principales y un identificador único por operación. La integración continua lo exporta, lo revisa con Spectral y lo guarda como artefacto.

## Version 1.0.91 (2026-10-27)

- Documentación de la API: el documento OpenAPI ahora tiene título, versión, esquema de seguridad Bearer, etiquetas por módulo, un resumen por cada una de las 75 operaciones y las respuestas de error comunes; se agrega la interfaz Swagger en `/swagger`. Solo se publica en desarrollo o con `OPENAPI_PUBLICAR=true`, y pruebas de contrato impiden dejar endpoints sin documentar.

## Version 1.0.90 (2026-10-27)

- Usuarios: «Mi perfil»: `GET /api/v1/perfil` y `PUT /api/v1/perfil` permiten a cualquier persona con sesión ver y cambiar su nombre, apellido y teléfono (el cambio se copia a Keycloak); el correo, los roles y el estado siguen gestionándose por administración.

## Version 1.0.89 (2026-10-27)

- Órdenes de trabajo: el listado acepta `soloActivas=true` y devuelve solo las órdenes activas (pendientes, en curso o por validar), las mismas que cuentan los conteos de clientes y usuarios.

## Version 1.0.88 (2026-10-27)

- Reportes: se elimina todo análisis por persona. Ya no existe el reporte de actividad por técnico (`actividad-tecnicos` responde `400 TIPO_REPORTE_INVALIDO`), los indicadores no traen `ordenesPorTecnico` y los reportes y la analítica no filtran por técnico (un `tecnicoId` se ignora).

## Version 1.0.87 (2026-10-27)

- Reportes: nuevo `GET /api/v1/reportes/desempeno` con el desempeño operativo de órdenes, procesos y maquinaria (órdenes por estado, tiempos de respuesta, ejecución y revisión, pausas y devoluciones con sus motivos, reasignaciones y horas de parada por tipo de máquina y por activo). No evalúa el desempeño de las personas.

## Version 1.0.86 (2026-10-26)

- Órdenes de trabajo: la orden informa quién la creó (`responsableId` y `nombreResponsable`).

## Version 1.0.85 (2026-10-26)

- Repuestos en órdenes: cada repuesto de una orden tiene cantidad. Al asignarlo queda reservado (ninguna otra orden puede usar esas unidades, pero siguen en la empresa), pasa a entregado cuando la orden entra en progreso y el stock baja recién al cerrar la orden, con un egreso a nombre de quien la cerró. Si la orden se elimina o se quita el repuesto, vuelve a estar libre.
- Stock: asignar más de lo disponible se rechaza con un mensaje según el caso (sin stock, reservado por otras órdenes o entregado). Un egreso o ajuste manual no puede dejar el stock por debajo de lo reservado y entregado. Las alertas de stock bajo siguen sobre el stock en la empresa.
- API: `repuestos` (repuesto y cantidad) reemplaza a `repuestosIds` en el alta y la modificación de órdenes; nuevo `PUT /api/v1/ordenes-trabajo/{id}/repuestos`; los repuestos informan `cantidadReservada`, `cantidadEntregada` y `cantidadDisponible`; la orden trae `historialRepuestos` con quién hizo cada cambio.
- Seguridad: el jefe de mantenimiento consulta Repuestos y, junto con el administrador y el pañolero, asigna repuestos y cambia cantidades en las órdenes; el técnico no.
- Base de datos: `ot_repuesto` guarda cantidad, estado y el movimiento que descuenta el stock; nueva tabla `historial_repuesto_ot`; los datos existentes se migran solos con 1 unidad y el estado de su orden. El dump exportado se regeneró.

## Version 1.0.84 (2026-10-26)

- Órdenes de trabajo: los estados pasan a ser `pendiente`, `en_progreso`, `en_revision`, `cerrado`, `pausado` y `eliminado` (baja lógica); asignar un técnico ya no es un estado. Los estados anteriores se convierten solos en el despliegue sin perder historial.
- Historial: cada cambio de estado guarda el estado anterior, la fecha y hora, quién lo hizo y el motivo, obligatorio al pausar, eliminar o devolver una orden de en revisión a pendiente. Cada asignación o cambio de técnico se guarda aparte para medir reasignaciones.
- Órdenes eliminadas: no admiten cambios (`409 ORDEN_NO_MODIFICABLE`), tampoco por `PUT`.
- Base de datos: nuevas columnas `id_estado_anterior` e `id_usuario` en `historial_estado_ot` y tabla `historial_tecnico_ot`; el dump exportado se regeneró.

## Version 1.0.83 (2026-10-26)

- Clientes: `GET /api/v1/clientes` acepta `conActivos=true` para devolver solo los clientes con al menos un activo asociado.

## Version 1.0.82 (2026-10-26)

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
- Base de datos: `sotra-dump.sql` regenerado con estos cambios.
- Herramienta `ComparadorIa` para comparar proveedores de IA con el corpus de evaluación.
- IA: la búsqueda de sugerencias usa solo la documentación del activo de la orden, de su modelo y la general (`activoId` en `POST /ordenes-trabajo/sugerencia`); ya no cita manuales de otras máquinas.
- IA: al procesar un PDF se quitan los encabezados y pies que se repiten en las páginas, para que una sección no se corte en el cambio de página. Los títulos en mayúsculas y los de procedimiento se reconocen, y los pasos numerados ya no se toman como títulos.
- IA: el proveedor redacta un título en español para la sugerencia (el título original queda en "Fuente") y responde `SIN_INFORMACION` si la sección no trata la tarea; en ese caso la API devuelve `422`. Las páginas de índice ya no se usan como respuesta.
- IA: búsqueda entre idiomas: con manuales en inglés, una sección con puntaje del reranker entre −2 y 0 se acepta si su distancia coseno a la consulta es de 0,40 o menos. Reglas de vocabulario técnico para la traducción.
- IA: límite de llamadas al proveedor para toda la API (`IA_API_LLAMADAS_POR_MINUTO`, 10 por defecto). La que se pasa usa el texto del manual con aviso (`modo = sin_ia`).
- Documentos técnicos: `POST /documentos-tecnicos/{id}/reprocesar` vuelve a procesar el PDF guardado con el divisor actual (`pdf-v5`), sin volver a subirlo.
- Base de datos: se elimina `orden_trabajo.id_recomendacion_ia`, que nada escribía (`database/cambios/2026-10-25-orden-sin-id-recomendacion-ia.sql`; los vínculos que hubiera pasan al historial).
- Datos: cliente SUPER CONSTRUCTURA SLA con 6 activos SANY y los modelos SY335, SY235 y SY155H (`database/cambios/2026-10-25-cliente-super-constructura-activos-sany.sql`).

## Version 1.0.81 (2026-10-26)

- Seguridad: un técnico ya no puede cerrar, cancelar ni devolver una orden; la API responde `403` (`TRANSICION_NO_PERMITIDA`) y solo le muestra los cambios de estado que le corresponden.

## Version 1.0.80 (2026-10-25)

- Órdenes de trabajo: el listado acepta el filtro `fase` y trae las órdenes de todos los estados de esa fase, además del filtro por `estado`.

## Version 1.0.79 (2026-10-25)

- Reportes y alertas: las órdenes abiertas, terminadas y pendientes de validación se calculan por la fase del estado y cada alerta por el código que define la base, sin nombres de estado en el código.

## Version 1.0.77 (2026-10-25)

- Órdenes de trabajo: las reglas de los estados (orden, fase, si exige técnico, qué fecha completa, color, alerta y estado inicial) y las transiciones (quién puede pasar a quién, si exige motivo y qué puede hacer un técnico) están ahora en la base, en `estado_ot` y `transicion_estado_ot`, y el código ya no nombra ningún estado.
- API: la orden devuelve `transiciones` en lugar de `estadosSiguientes`, y el catálogo de estados suma `orden`, `fase` y `tono`.
- Base de datos: SQL incremental y `01-init.sql` con las columnas y la tabla nuevas; el despliegue se detiene con un mensaje claro si hay estados sin reglas. Se actualiza `sotra-dump.sql`.

## Version 1.0.76 (2026-10-25)

- Órdenes de trabajo: los estados y sus etiquetas salen de la tabla `estado_ot` y los entrega `GET /api/v1/ordenes-trabajo/estados`, en el orden del ciclo de vida; si una etiqueta falta, se arma una legible.
- Base de datos: el catálogo `estado_ot` incluye `cancelada` y las etiquetas, en `01-init.sql` y en el cambio para bases existentes, que se aplica solo en el despliegue.
- Base de datos: se agrega `sotra-dump.sql` en la raíz, un dump sin PDF ni embeddings.

## Version 1.0.75 (2026-10-25)

- Documentación: el README resume lo que hay que hacer para pasar a producción: aplicar los SQL de `database/cambios/` en el despliegue.

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
