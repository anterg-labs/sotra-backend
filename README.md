# Backend de SOTRA

## Requisitos

### Linux

Docker Engine con el plugin `docker compose`. El usuario debe pertenecer al grupo `docker` (`sudo usermod -aG docker $USER` y volver a iniciar sesión).

### Windows

Docker Desktop. Los comandos `docker compose` son los mismos; se pueden ejecutar desde PowerShell o Git Bash.

Los archivos de `database/init/` deben tener saltos de línea LF; `.gitattributes` lo fuerza al clonar.

## Configurar PostgreSQL con el archivo `.env`

1. Copiar `.env.example` como `.env` en la raíz de `sotra-backend/`.

Linux:

```bash
cp .env.example .env
```

Windows (PowerShell):

```powershell
Copy-Item .env.example .env
```

2. Configurar la conexión local sin publicar las credenciales:

```env
DB_HOST=localhost
DB_PORT=5432
DB_NAME=sotra
DB_USER=TU_USUARIO
DB_PASSWORD=TU_PASSWORD
FRONTEND_ORIGIN=http://localhost
```

Esta configuración permite ejecutar el backend directamente en el equipo (Linux o Windows) contra PostgreSQL publicado en el puerto estándar `5432`.

`compose.yml` conserva la configuración original de producción con PostgreSQL y el backend. La configuración de desarrollo está separada en `compose.dev.yml` y carga las variables desde `sotra-backend/.env`.

El archivo `.env` no se debe versionar. `.env.example` se conserva como plantilla sin credenciales reales.

## Ejecutar directamente en el equipo

Con el SDK de .NET instalado (Linux, macOS o Windows). Comprobar la compilación:

```bash
dotnet build
```

Comprobar Entity Framework:

```bash
dotnet ef dbcontext info --context SotraContext
```

## Levantar PostgreSQL y el backend con Docker

### Producción local

Construir y levantar la configuración original de producción en segundo plano:

```bash
docker compose up -d --build
```

Este comando usa `compose.yml`, levanta PostgreSQL y el backend, y deja la API disponible en `http://localhost:8080`.

#### Carga automática de datos

Antes de iniciar el backend, `compose.yml` ejecuta el servicio de una sola vez `db-datos` (`database/init/cargar-datos.sh`), que compara el SHA-256 de `database/init/01-init.sql` con el último que cargó, guardado en la tabla `public.carga_datos`.

- **Sin diferencias:** no hace nada.
- **Con diferencias o primera vez:** borra y recrea el esquema `operativo` con la estructura y los datos de `01-init.sql` (clientes, activos, usuarios, roles, permisos, órdenes, repuestos, etc.). Los cambios hechos a mano en `operativo` se pierden.
- **Esquema `vectorial`:** `02-vectorial.sql` crea solo su estructura vacía, y únicamente si el esquema no existe. Si ya existe, no se toca. Los PDF, fragmentos y embeddings no vienen en el SQL; se generan con la asistencia documental.

Las contraseñas de usuario que no eran un hash de ASP.NET Identity se reemplazaron por `REEMPLAZAR_POR_HASH_ASPNET_IDENTITY`: esos usuarios no pueden iniciar sesión hasta que se les asigne una contraseña.

Para actualizar los datos, editar `01-init.sql` y volver a ejecutar `docker compose up -d`. Para ver qué hizo la carga: `docker compose logs db-datos`.

### Desarrollo local

La configuración de desarrollo usa la base local publicada en `localhost:5432`. Si se utiliza `sotra-postgres-local`, debe iniciarse antes:

```bash
docker start sotra-postgres-local
docker compose -f compose.dev.yml up -d --build
```

La API queda disponible en `http://localhost:8081`. El código fuente está montado dentro del contenedor y `dotnet watch --no-hot-reload` reinicia la aplicación al guardar cambios. Así se evita que la recarga en caliente intente leer archivos temporales de recursos estáticos que no existen cuando la carpeta se monta desde Windows. Esta configuración no modifica ni reemplaza el `Dockerfile` ni el `compose.yml` de producción.

Servicios expuestos al equipo:

- PostgreSQL: host `localhost` y puerto `5432`.
- Backend de producción: `http://localhost:8080`.
- Backend de desarrollo: `http://localhost:8081`.

Comprobar la conexión:

```bash
docker exec sotra-postgres-local pg_isready -U sotra -d sotra
curl http://localhost:8080/api/v1/roles
curl http://localhost:8081/api/v1/roles
```

## Contratos de modificación

Los endpoints `PUT` reciben todos los campos editables del recurso. No funcionan como modificaciones parciales.

En usuarios son obligatorios `nombre`, `apellido`, `correo`, `telefono`, `idsRol`, `estado` y `fechaIngreso`. `contrasena` puede omitirse para conservar la contraseña actual.

En activos, `estado` es opcional y acepta `activo` o `inactivo`. Si se omite, el estado no cambia; cualquier otro valor responde `400`. Permite reactivar un activo dado de baja. El cliente y el modelo de máquina deben estar activos.

En tipos de máquina, `nombre` es obligatorio. En modelos de máquina son obligatorios `nombre` y `tipoMaquinaId`; `descripcion` acepta texto o `null`. En los dos, `estado` es opcional y acepta `activo` o `inactivo`: si se omite, el estado no cambia, y cualquier otro valor responde `400`. Así se puede reactivar un tipo o un modelo dado de baja.

`PATCH /api/v1/clientes/{id}` y `PATCH /api/v1/repuestos/{id}` aceptan `{ "estado": "inactivo" }` para la baja lógica y `{ "estado": "activo" }` para reactivar (`ClienteDAO.ReactivarCliente` y `RepuestoDAO.Reactivar`). Los `PATCH` de tipos, modelos, activos y usuarios siguen aceptando solamente `inactivo`. `PUT /api/v1/repuestos/{id}` ignora `cantidad`: el stock solo cambia con ingresos, egresos y ajustes.

### Campos opcionales de clientes

En `POST /api/v1/clientes` y `PUT /api/v1/clientes/{id}`, `razonSocial` y `nombreContacto` son obligatorios. `correo`, `telefono`, `direccion` y `observaciones` aceptan texto o `null`.

El `PUT` recibe los seis campos editables. Enviar `null` en uno de los cuatro campos opcionales elimina su valor anterior. El frontend debe enviar `null` en lugar de una cadena vacía cuando el campo no tenga valor.

## Mensajes de error

Los controladores responden los errores esperados (`400`, `409`, `422`) con un objeto que incluye `mensaje`, un texto en español pensado para el usuario. El frontend muestra ese `mensaje` tal como llega, así que no debe contener datos técnicos. Por eso las `ArgumentException` se crean solo con el texto, sin el nombre del parámetro: si se agrega, .NET suma al mensaje un texto como `(Parameter 'idCliente')`. Los errores automáticos de validación ahora traen `codigo` (`VALIDACION_FALLIDA`) y un `mensaje` con el texto del primer campo inválido.

Un manejador global (`Api/Infraestructura/ManejadorGlobalDeErrores.cs`) cubre lo que no captura un controlador: `ArgumentException` a `400`, `ConflictoDeDatosException` a `409`, violación de unicidad a `409` y cualquier otra excepción a `500` con un mensaje genérico, sin trazas ni SQL. Para un conflicto con código propio, el DAO lanza `ConflictoDeDatosException(codigo, mensaje)`.

## Seguridad de la API sin sesión

- **Cabeceras de seguridad** en todas las respuestas (`CabecerasDeSeguridadMiddleware`): `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`, `Permissions-Policy`, `Cross-Origin-Resource-Policy` y `Cache-Control: no-store` en `/api`. HSTS y la redirección a HTTPS se activan fuera de desarrollo.
- **Límite de solicitudes** por dirección IP (`Api/Infraestructura/LimiteDeSolicitudes.cs`), con ventana deslizante de un minuto: 1200 en toda la API, 300 para altas y modificaciones, 30 para subidas de archivos y 20 para las sugerencias de IA. Superarlo devuelve `429` con `codigo` `DEMASIADAS_SOLICITUDES` y `Retry-After`. Se ajusta con la sección `LimiteSolicitudes` o con variables como `LimiteSolicitudes__PorMinutoGeneral`, y se apaga con `LimiteSolicitudes__Habilitado=false`.
- **Caracteres nulos:** `EntradaSinCaracteresNulosMiddleware` y `TextoSinCaracteresNulosConverter` rechazan `\0`/`%00` en la ruta, la consulta, el JSON y los formularios con `400` `ENTRADA_INVALIDA` (antes llegaban a PostgreSQL y daban `500`). La cabecera `Server` está desactivada (`AddServerHeader = false`).
- **Detrás de un proxy** (Nginx o Cloudflare) hay que definir `PROXIES_CONFIANZA` con la dirección o red del proxy; si no, todas las personas comparten el límite del proxy.
- **CORS** acepta solo el origen de `FRONTEND_ORIGIN`, con los métodos y headers que usa el frontend.

## Autenticación preparada y apagada

La integración con Keycloak (login, tokens, usuarios) la implementa otra persona del equipo. Aquí solo queda la API lista para recibir el token.

- **Interruptor:** `Seguridad__Habilitada` (por defecto `false`). Apagada, la API responde igual que antes y no exige nada.
- **Encendida:** valida un token JWT `Bearer` contra el emisor `URL_KEYCLOAK` (debe ser `https`, salvo bucle local) y la audiencia `ID_CLIENTE_KEYCLOAK`, y exige una persona autenticada en todos los endpoints salvo `/health`, `/health/ready` y `/api/v1/version`. Sin token responde `401` y sin permiso `403`, ambos como `ProblemDetails` con `codigo` (`NO_AUTENTICADO` o `SIN_PERMISO`).
- **Permisos:** los endpoints llevan `[Permiso("...")]` con los nombres de la tabla `permiso` (por ejemplo `usuario.crear`, `ot.asignar_tecnico`, `stock.registrar_movimiento`). Se leen de la claim `Seguridad__ClaimPermisos` (por defecto `permisos`, valores separados por espacio). El contrato real del token se define con quien integra Keycloak.
- **Persona actual:** `IUsuarioActual` expone si hay una persona autenticada y su correo (claim `Seguridad__ClaimCorreo`, por defecto `email`). Con la seguridad apagada siempre devuelve "sin autenticar".
- **Sin cubrir todavía:** la base solo define 14 permisos, que alcanzan para usuarios, activos, consulta y alta de modelos, órdenes, evidencias y stock. Clientes, tipos de máquina, modificación y baja de modelos, alta y modificación de repuestos, documentos técnicos y reportes no tienen permiso definido y, con la seguridad encendida, solo exigen estar autenticado.

## Errores de negocio centralizados

Los controladores ya no repiten bloques `catch`. Las reglas de negocio lanzan excepciones tipadas (`ValidacionDeNegocioException` con su `Codigo`, `ConflictoDeDatosException`, `ErrorDeOrdenException`) y las violaciones de restricciones únicas de PostgreSQL llegan como `DbUpdateException`; `Api/Infraestructura/TraduccionDeErroresDeNegocio.cs` decide estado HTTP, `codigo` y texto en un único lugar (tabla por restricción y, si falta, por tabla) y `ManejadorGlobalDeErrores` lo responde como `ProblemDetails` con `codigo` y `mensaje`. Para agregar un duplicado nuevo basta una fila en esa tabla. Pruebas: `Api.PruebasUnitarias/TraduccionDeErroresPruebas.cs`. Los `catch` que quedan (documentos técnicos y sugerencias de IA) distinguen cancelación, servicio de IA caído y error inesperado, y dejan pasar las excepciones de negocio.

## Términos y condiciones

Un único documento editable, sin historial. `GET /api/v1/terminos/vigente` devuelve el texto y su marcador interno de cambios. `POST /api/v1/terminos` recibe únicamente `{ texto }` y responde `200`, modificando el mismo registro; valida de 20 a 20 000 caracteres. Si cambia el texto se incrementa el marcador y se reinician sus aceptaciones. Guardar sin cambios las conserva. No hay endpoint de historial, ni campos de título, datos o autor en el contrato.

La aceptación por persona exige `{ usuarioId, terminosId, version, acepta: true }`; es idempotente y rechaza contenido desactualizado con `409 TERMINOS_DESACTUALIZADOS`. Guardado y aceptación se serializan con bloqueo transaccional de PostgreSQL. La auditoría existente obtiene al autor de `IUsuarioActual`, sin permitir que el formulario lo elija; hasta Keycloak registra «sin autenticar». El frontend usa un único editor visual y comparte el mismo modal para la vista previa y el footer; temporalmente recuerda la aceptación por navegador.

No se agregan tablas. Las bases nuevas exigen un único registro mediante `CHECK (vigente)` y el índice único parcial. Para bases existentes se entrega `database/cambios/2026-10-17-terminos-documento-unico.sql`, que elimina el historial antiguo y conserva el texto vigente. **No aplicado a la base local ni al VPS: requiere ejecución manual autorizada.** No se reutilizan las columnas heredadas de título, datos separados o autor en el contrato. Detalles en `Documentacion/Flujo de datos SOTRA.md`, sección 15 bis.

## Observabilidad (OpenTelemetry)

Trazas y métricas con OpenTelemetry, **apagadas por defecto**: no cambian nada hasta definir un destino. Se activan con `OTEL_EXPORTER_OTLP_ENDPOINT` (por ejemplo el receptor OTLP de Grafana Alloy del VPS, `http://127.0.0.1:4318` con `OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf`) o con `Observabilidad__Habilitada=true`; se apagan con `Observabilidad__Habilitada=false`. El destino, los encabezados de autenticación, el muestreo y el nombre del servicio (`OTEL_SERVICE_NAME`, por defecto `sotra-api`) usan las variables estándar `OTEL_*`. Código en `Api/Infraestructura/Observabilidad.cs`.

- **Trazas:** cada solicitud HTTP entrante (sin `/health`), las llamadas salientes (`HttpClient`, Ollama) y cada comando a PostgreSQL (fuente `Npgsql`).
- **Métricas estándar:** ASP.NET Core (solicitudes, duración, errores), `HttpClient`, tiempo de ejecución .NET (GC, hilos, memoria), Npgsql (conexiones, comandos) y EF Core.
- **Métricas de SOTRA** (medidor `Sotra.Api`): `sotra.acciones` (altas, modificaciones, bajas, movimientos y cambios de estado, por recurso, acción y clase de resultado), `sotra.sugerencias_ia.duracion` (histograma en segundos, por resultado `ok`, `sin_contexto`, `no_disponible` o `error`), `sotra.reportes_generados` (por tipo y formato) y `sotra.entradas_rechazadas`.
- **Logs:** cada línea lleva `IdCorrelacion` y `TraceId`, para pasar de un log a su traza.
- Se comprobó contra un colector OTLP local: llegan trazas con spans HTTP y de base de datos, y métricas con `sotra.acciones`. Las pruebas unitarias están en `Api.PruebasUnitarias/ObservabilidadPruebas.cs`.

## Logs

El backend usa Serilog. En desarrollo escribe texto legible en la consola; en los demás ambientes escribe JSON compacto (una línea por evento), que Docker recoge. Los niveles se ajustan en la sección `Serilog` de `appsettings.json`. Cada solicitud se registra con método, ruta (sin parámetros de consulta), estado, duración y dirección IP, y lleva un `IdCorrelacion` que también se devuelve en la cabecera `X-Correlation-Id`. No se registran contraseñas, tokens ni el contenido de los archivos.

## Repuestos, órdenes y documentos técnicos

La API implementa los módulos restantes que consume el frontend:

- `/api/v1/repuestos`: `GET`, `GET /alertas-stock`, `GET /{id}`, `POST`, `PUT /{id}`, `PATCH /{id}`, `PATCH /{id}/stock-minimo` y `POST /{id}/ingresos`, `/egresos` y `/ajustes`.
- `/api/v1/ordenes-trabajo`: `GET`, `GET /{id}`, `POST`, `PUT /{id}`, `PATCH /{id}/estado`, `PATCH /{id}/tecnico` y `POST /sugerencia`. El `GET` es paginado y acepta búsqueda y filtros (ver "Listados paginados").
- `/api/v1/documentos-tecnicos`: `GET`, `POST`, `GET /{id}/archivo` y `DELETE /{id}`.
- `/api/v1/reportes/indicadores`: `GET`.

Las órdenes usan las tablas existentes del esquema `operativo`. La fecha prevista se conserva en la observación del primer registro de `historial_estado_ot`, y las sugerencias se guardan en `historial_recomendaciones_ia`. El responsable (`id_usuario_responsable`) es el jefe de mantenimiento: hasta que exista el login, `OrdenTrabajoDAO.Crear` asigna el primer usuario activo con el rol activo "Jefe de mantenimiento" y responde `400` si no hay ninguno. `Actualizar` conserva el responsable de la orden.

Los documentos se guardan en la tabla `vectorial.documento_tecnico`, creada por la API cuando hace falta. Todo el módulo de documentos y sugerencias accede a PostgreSQL con SQL directo (Npgsql) y no con Entity Framework, porque las tablas `vectorial.*` no forman parte del modelo de EF y usan el tipo `vector` de pgvector. Por eso también la validación del activo asociado se hace con SQL sobre `operativo.activo`; el resto de los módulos (usuarios, clientes, activos, repuestos y órdenes) usa Entity Framework. El archivo se valida por su firma `%PDF-`; si se marca para sugerencias, PdfPig extrae el texto. Al procesar un documento para IA, el texto se divide en fragmentos y cada uno se guarda con su embedding de `bge-m3` en `vectorial.fragmentos` (pgvector).

### Versión del backend (`GET /api/v1/version`)

La versión está en `version.json`, en la raíz del repositorio, con formato `x.y.z`. `VersionController` la lee y responde `{ "version": "x.y.z" }`; el frontend la muestra en el pie de página.

Durante el plan, cada entrega local incrementa el parche manualmente si no interviene el hook, evitando dos incrementos por la misma entrega (`AGENTS.md`). `Api.csproj` incluye el archivo en `Watch`: `dotnet watch` detecta sus modificaciones y copia la versión actual al directorio de ejecución. Verificar tanto el archivo como `GET /api/v1/version`, porque el controlador lee la copia de salida de la API.

- **`z` (parche):** sube sola en cada commit. La sube el hook `.githooks/pre-commit`, que modifica `version.json` y lo agrega al commit.
- **`x` e `y`:** solo se cambian a mano, cuando lo decide el responsable. Si en un commit `x` o `y` son distintas de las del último commit, el hook respeta la versión escrita y no sube la `z` (por ejemplo, para pasar a `1.1.0`).

Los hooks de `.githooks/` solo funcionan si cada integrante los activa una vez en su copia del repositorio:

```bash
git config core.hooksPath .githooks
```

Ese mismo hook también ejecuta `dotnet format --verify-no-changes` (solo informa, no bloquea) y `gitleaks protect --staged` (bloquea el commit si encuentra secretos). Los commits que no pasan por `pre-commit` (por ejemplo, `git cherry-pick` o un merge desde GitHub) no suben la versión.

### Listados paginados

Siguen las secciones 9.1 y 9.2 de los criterios de desarrollo. Responden `PaginaDto<T>` (`Api/DTOs/PaginaDto.cs`): `elementos`, `pagina`, `tamanoPagina`, `totalElementos` y `totalPaginas`. Reciben `pagina` (desde 1, por defecto 1) y `tamanoPagina` (de 1 a 100, por defecto 20); fuera de rango responden `400` con `PAGINACION_INVALIDA`. Los filtros llegan por query string en un DTO de filtros por módulo y se aplican en memoria en el controlador, después de `ListarTodo` del DAO, igual que en los indicadores.

Listados paginados hasta ahora:

- `GET /api/v1/ordenes-trabajo` (`FiltrosOrdenesTrabajoDto`): `buscar` (número exacto, o texto en descripción, cliente, activo o técnico, sin distinguir mayúsculas), `clienteId`, `activoId`, `tecnicoId`, `estado`, `prioridad`, `tipo`, `fechaDesde` y `fechaHasta` (fecha de creación; invertidas responden `400` con `RANGO_FECHAS_INVALIDO`), `ordenarPor` (`id` o `creadoEn`) y `direccionOrden` (`asc` o `desc`).

- `GET /api/v1/activos` (`FiltrosActivosDto`): `buscar` (nombre, número de serie, ubicación o descripción), `clienteId`, `modeloMaquinaId` y `estado`.
- `GET /api/v1/modelos-maquina` (`FiltrosModelosMaquinaDto`): `buscar` (nombre o descripción), `tipoMaquinaId` y `estado`.
Los listados y detalles paginados de activos incluyen `nombreCliente` y `nombreModeloMaquina`; los de modelos incluyen `nombreTipoMaquina`. Se obtienen con relaciones en la misma consulta, para que el frontend no recorra todas las páginas de los catálogos al mostrar las tablas. Tras un alta o una modificación estos nombres pueden ser `null` hasta volver a consultar el recurso.
- `GET /api/v1/tipos-maquina` (`FiltrosTiposMaquinaDto`): `buscar` (nombre) y `estado`.
- `GET /api/v1/documentos-tecnicos` (`FiltrosDocumentosTecnicosDto`): `buscar` (nombre del archivo o del activo) y `activoId`. Además existe `GET /api/v1/documentos-tecnicos/{id}`, que devuelve un documento sin el archivo (`404` si no existe).

- `GET /api/v1/usuarios` (`FiltrosUsuariosDto`): `buscar` (nombre, apellido o correo), `rolId`, `estado` y `direccionOrden` (`desc` invierte el orden, para ver primero los más nuevos).
- `GET /api/v1/clientes` (`FiltrosClientesDto`): `buscar` (razón social, contacto o correo), `estado` y `direccionOrden`.
- `GET /api/v1/repuestos` (`FiltrosRepuestosDto`): `buscar` (código, nombre, marca o descripción), `estado`, `ubicacion` (contiene) `bajoStock=true` (stock bajo o sin stock), `estadoStock` (`normal`, `bajo` o `sin_stock`) y `orden=criticidad`. Cada repuesto trae `estadoStock`, calculado por `EstadosStock.Calcular` (sin stock con cantidad 0; bajo si el mínimo es mayor que 0 y la cantidad lo alcanza).
- `POST` y `PUT /api/v1/repuestos`: si el código ya existe, responden `409` con `codigo: "CODIGO_REPUESTO_DUPLICADO"` y un mensaje legible. El frontend sitúa el error junto al campo Código.
- `GET /api/v1/movimientos-stock` (`FiltrosMovimientosStockDto`): `buscar` (código o nombre del repuesto, o motivo), `repuestoId`, `tipo`, `fechaDesde`, `fechaHasta`, `ubicacion` y `usuarioId`. Ordenados del más nuevo al más viejo.

`PaginaDto` también tiene `ErrorDePaginacion()` (el cuerpo del `400`) y `Contiene(texto, buscado)` (comparación sin distinguir mayúsculas), que usan todos los controladores paginados.

### Movimientos de stock (`/api/v1/movimientos-stock` y `POST /api/v1/repuestos/{id}/ingresos`)

RF004-02 y RF004-07. **Requiere ampliar la tabla `operativo.movimiento_stock`:** el SQL sugerido está en `SUGERENCIAS-SQL.md` (agrega `tipo`, `motivo`, `ubicacion`, `observaciones`, `stock_anterior` y `stock_posterior`, pasa `cantidad` a entero y deja `id_usuario` opcional). Hasta aplicarlo, estos endpoints responden `500`; el resto de repuestos funciona. Modelo `MovimientoStock` (actualizado), `MovimientoStockDAO`, `MovimientoStockMapeador` y `MovimientosStockController`.

- `POST /api/v1/repuestos/{id}/ingresos`: recibe `{ "cantidad", "fecha" (AAAA-MM-DD), "motivo", "ubicacion", "observaciones" }`. Mediante `MovimientoStockDAO.Registrar`, en una transacción que bloquea la fila del repuesto (`SELECT ... FOR UPDATE`), suma la cantidad y registra un movimiento `ingreso` con el stock anterior y posterior. Responde `201` con el movimiento. Errores: `404` si el repuesto no existe; `409` `REPUESTO_INACTIVO`; `400` `CANTIDAD_INVALIDA` (debe ser mayor a cero), `FECHA_INVALIDA` (vacía o futura) y `DATOS_OBLIGATORIOS` (motivo y ubicación). Si la fecha es hoy se guarda la hora actual; si es otro día, ese día a las 12:00 UTC.
- `POST /api/v1/repuestos/{id}/egresos` (`cantidad`, `motivo`, `ubicacion?`, `observaciones?`) y `POST /api/v1/repuestos/{id}/ajustes` (`cantidadReal`, `motivo`, `observaciones?`) usan el mismo método y bloqueo. `409` `STOCK_INSUFICIENTE` si el egreso supera el stock, `409` `REPUESTO_INACTIVO`, `400` `CANTIDAD_INVALIDA` y `DATOS_OBLIGATORIOS`. En un ajuste la `cantidad` del movimiento es la diferencia con signo.
- `PATCH /api/v1/repuestos/{id}/stock-minimo` cambia solo `cantidadMinima` (entero mayor o igual a 0). `GET /api/v1/repuestos/alertas-stock` devuelve `{ bajo, sinStock, total }` de repuestos activos, contados en la base.
- Restricción de base: `database/cambios/2026-10-16-repuesto-stock-no-negativo.sql` (aplicar a mano) impide `cantidad` y `cant_min` negativos; `01-init.sql` ya la incluye para bases nuevas.
- `GET /api/v1/movimientos-stock` (paginado, ver arriba) y `GET /api/v1/movimientos-stock/{id}`: cada movimiento tiene `id`, `repuestoId`, `codigoRepuesto`, `nombreRepuesto`, `tipo`, `cantidad`, `fecha`, `ubicacion`, `usuarioId`, `nombreUsuario`, `motivo`, `observaciones`, `stockAnterior` y `stockPosterior`.

La especificación también pide filtrar repuestos por "categoría", pero el repuesto no tiene ese dato en la base; queda pendiente de definir.

### Ciclo de vida de la orden (`PATCH /api/v1/ordenes-trabajo/{id}/estado` y `/tecnico`)

RF005-06 y RF005-07, según el ciclo de vida del anteproyecto. Las transiciones permitidas están en `OrdenTrabajoDAO.TransicionesPermitidas` y se validan tanto en `PATCH /estado` como en `PUT`:

| Estado actual | Puede pasar a |
|---|---|
| `creada` | `asignada`, `cancelada` |
| `asignada` | `en_ejecucion`, `cancelada` |
| `en_ejecucion` | `pausada`, `finalizada` |
| `pausada` | `en_ejecucion`, `cancelada` |
| `finalizada` | `cerrada` (validación del jefe), `asignada` (reasignación) |
| `cerrada`, `cancelada` | ninguno |

- `PATCH /{id}/estado` recibe `{ "estado": "...", "motivo": "..." }`. El motivo es obligatorio al pausar, cancelar, cerrar o reasignar una orden finalizada, y se guarda en `historial_estado_ot.observacion` (no hace falta cambiar la base). `asignada` y `en_ejecucion` exigen técnico asignado. `PUT` acepta el mismo motivo en `motivoEstado`.
- `PATCH /{id}/tecnico` recibe `{ "tecnicoId": 0 }`. Valida que sea un usuario activo con rol Técnico, pasa la orden de `creada` a `asignada` y registra en el historial "Técnico asignado: Nombre Apellido".
- Errores con `codigo`: `400` `TRANSICION_INVALIDA`, `TECNICO_OBLIGATORIO` o `MOTIVO_OBLIGATORIO`; `409` `ORDEN_CERRADA` (cambiar una orden cerrada) u `ORDEN_NO_MODIFICABLE` (asignar técnico a una orden cerrada o cancelada); `404` si la orden no existe.
- Ediciones simultáneas: `OrdenTrabajoDto.Version` (hash SHA-256 truncado del DTO, calculado en `OrdenTrabajoMapeador`) se devuelve también como `ETag` en `GET /ordenes-trabajo/{id}` y `PUT`. El `PUT` acepta `If-Match` y lo compara dentro del bloqueo de fila de `OrdenTrabajoDAO.Actualizar`; si no coincide responde `412` `ORDEN_MODIFICADA` sin cambiar nada. Sin el encabezado (o con `*`) no se verifica. CORS permite `If-Match` y expone `ETag`.
- `OrdenTrabajoDto` incluye `estadosSiguientes` (los estados a los que puede pasar) y `historialEstados` (`estado`, `fechaHora` en UTC y `observacion`, del más viejo al más nuevo; la observación del primer registro no se muestra porque guarda la fecha prevista).

El historial todavía no registra el usuario responsable del cambio, porque no hay login.

### Evidencias (`/api/v1/ordenes-trabajo/{id}/evidencias`)

RF005-05. **Requiere la tabla `operativo.evidencia_ot`, que todavía no existe:** el SQL sugerido está en `SUGERENCIAS-SQL.md` (raíz del directorio de trabajo) y hay que aplicarlo antes de usar estos endpoints. Modelo `EvidenciaOt`, mapeo en `SotraContext`, `EvidenciaOtDAO` y `EvidenciasController`.

- `GET`: lista las evidencias de la orden, sin el contenido del archivo. Cada una tiene `id`, `ordenTrabajoId`, `tipo`, `observacion`, `fechaRegistro` (`AAAA-MM-DD`), `creadoEn` (UTC), `nombreArchivo`, `tipoContenido` y `tieneArchivo`.
- `POST` (`multipart/form-data`): campos `tipo` (`foto`, `documento` u `observacion`), `observacion`, `fechaRegistro` y `archivo`. Responde `201`. Reglas: la orden no puede estar cerrada ni cancelada (`409` `ORDEN_NO_MODIFICABLE`); `foto` y `documento` exigen archivo (`ARCHIVO_OBLIGATORIO`) y `observacion` exige texto (`OBSERVACION_OBLIGATORIA`); la fecha no puede ser futura (`FECHA_REGISTRO_INVALIDA`); tipo desconocido: `TIPO_EVIDENCIA_INVALIDO`. El archivo se valida por su contenido real (firma de JPG, PNG o PDF), no por la extensión: las fotos solo JPG o PNG y los documentos PDF, JPG o PNG (`ARCHIVO_NO_PERMITIDO`); hasta 10 MB (`413` `ARCHIVO_DEMASIADO_GRANDE`).
- `GET /{evidenciaId}/archivo`: descarga el archivo con su tipo de contenido.

El usuario que registra la evidencia (`id_usuario`) queda vacío hasta que exista el login.

### Registro de sugerencias de IA (RF006-05)

El equipo decidió que la validación se hace en el formulario de la orden: las sugerencias que quedan en la orden son las aceptadas y las que el usuario quitó son las descartadas (ver `ACLARACIONES-FUNCIONALIDADES.md`). Todas quedan registradas en `historial_recomendaciones_ia` para analizar el modelo; este historial no se muestra al usuario. Requiere las columnas del punto 4 de `SUGERENCIAS-SQL.md` (ya aplicadas en la base local).

- `POST /api/v1/ordenes-trabajo/sugerencia` recibe además `sesion` (UUID que crea el formulario). Después de generar la sugerencia, `OrdenTrabajoDAO.RegistrarSugerenciaGenerada` la guarda sin orden, con `sesion`, `consulta` (la descripción), `numero_intento`, `fecha_generacion` y `resultado = pendiente`.
- `POST` y `PUT` de órdenes reciben `sesion`. Al guardar, `ReemplazarSugerencias` asocia a la orden las sugerencias pendientes de esa sesión: las que siguen en la lista quedan `aceptada` (y `estado = true`), el resto `descartada`. Las sugerencias que ya tenía la orden y no cambiaron se conservan; las quitadas al editar pasan a `estado = false`.
- Las sugerencias de formularios abandonados quedan con `resultado = pendiente` y sin orden.
- `GET /api/v1/ordenes-trabajo` acepta `conSugerencias=true` para listar solo las órdenes con recomendaciones.

### Indicadores (`GET /api/v1/reportes/indicadores`)

Dashboard de RF007-01, sin permisos. `ReportesController` no tiene DAO propio: usa `OrdenTrabajoDAO.ListarTodo` y `RepuestoDAO.ListarTodo`, pasa cada orden por `OrdenTrabajoMapeador.ADto` (así el estado, la prioridad y el tipo son los mismos textos que devuelve `/ordenes-trabajo`) y cuenta en memoria.

Respuesta: `totalOrdenes`; `ordenesPorEstado`, `ordenesPorPrioridad`, `ordenesPorTipo`, `ordenesPorTecnico` y `activosConMasIntervenciones` como listas de `{ "nombre": "...", "cantidad": 0 }`; y `repuestosBajoStock` como lista de `RepuestoDto`. Solo aparecen los grupos con al menos una orden. Las órdenes sin técnico no se cuentan por técnico. Los activos se ordenan de mayor a menor y se devuelven los 5 primeros. Un repuesto tiene bajo stock si está activo y su `cantidad` es menor o igual a su `cantidadMinima`.

También devuelve `promedioHorasHastaInicio` y `promedioHorasHastaCierre` (horas promedio entre la creación y `fechaInicio` o `fechaCierre`, con un decimal; `null` si ninguna orden tiene esa fecha), `ordenesAltaPrioridadPendientes` (prioridad `alta` y estado distinto de `finalizada`, `cerrada` y `cancelada`), `ordenesPendientesDeValidacion` (estado `finalizada`) y `ordenesRecientes` (las 5 órdenes más nuevas, con el mismo formato que `GET /ordenes-trabajo`).

Filtros opcionales por query string (`FiltrosIndicadoresDto`), que se pueden combinar:

| Parámetro | Tipo | Filtra las órdenes por |
|---|---|---|
| `fechaDesde` | fecha `AAAA-MM-DD` | Fecha de creación mayor o igual |
| `fechaHasta` | fecha `AAAA-MM-DD` | Fecha de creación menor o igual |
| `tecnicoId` | número | Técnico asignado |
| `clienteId` | número | Cliente |
| `activoId` | número | Activo |
| `estado` | texto | Estado, con los mismos valores que `/ordenes-trabajo` (`creada`, `asignada`, `en_ejecucion`, `pausada`, `finalizada`, `cerrada`, `cancelada`) |
| `tipo` | texto | `preventivo` o `correctivo` |
| `prioridad` | texto | `baja`, `media` o `alta` |

Los filtros se aplican a todos los indicadores de órdenes. `repuestosBajoStock` no depende de los filtros, porque no está relacionado con las órdenes. Un parámetro vacío o ausente no filtra. Si `fechaDesde` es posterior a `fechaHasta`, responde `400` con `{ "codigo": "RANGO_FECHAS_INVALIDO", "mensaje": "..." }`. Ejemplo en `Api/Api.http`.

### Descarga de reportes en PDF y CSV (`GET /api/v1/reportes/{tipoReporte}/pdf` y `/csv`)

RF007-02. Devuelve un archivo `application/pdf` llamado `reporte-{tipoReporte}.pdf`, generado con la librería QuestPDF (licencia Community, configurada al inicio de `Program.cs`). El diseño del documento está en `Api/Servicios/GeneradorReportesPdf.cs`: título, fecha de generación (hora de Uruguay), criterios aplicados, tablas y número de página. `GET /api/v1/reportes/{tipoReporte}/csv` devuelve las mismas tablas (se arman una sola vez en `ReportesController.ArmarReporte`) como `text/csv` UTF-8 con BOM y separador `;`; `Api/Servicios/GeneradorReportesCsv.cs` aplica las comillas de RFC 4180 y antepone `'` a las celdas que empiezan con `=`, `+`, `-`, `@`, tabulación o retorno de carro (inyección de fórmulas).

Tipos de reporte (`tipoReporte`):

| Valor | Contenido |
|---|---|
| `ordenes-trabajo` | Una fila por orden: número, fecha de creación, cliente, activo, técnico, tipo, prioridad y estado |
| `activos-intervenidos` | Activos con su cliente y la cantidad de órdenes |
| `repuestos-utilizados` | Repuestos asociados a las órdenes y en cuántas órdenes se usó cada uno |
| `actividad-tecnicos` | Por técnico: órdenes asignadas, finalizadas o cerradas, y pendientes |
| `indicadores` | Resumen (total y tiempos promedio), órdenes por estado, prioridad y tipo, actividad por técnico y activos intervenidos |

Acepta los mismos filtros que `/indicadores` y además `prioridad` (`baja`, `media` o `alta`), que también vale para `/indicadores`. Errores: `400` con `TIPO_REPORTE_INVALIDO` si el tipo no existe y `RANGO_FECHAS_INVALIDO` si las fechas están invertidas.

Al agregar el paquete, el contenedor `sotra-backend-dev` necesita reiniciarse (`docker restart sotra-backend-dev`) para restaurarlo.

### Sugerencias (`POST /api/v1/ordenes-trabajo/sugerencia`)

El código está en `Api/Servicios/`, un archivo por responsabilidad:

| Archivo | Qué hace |
|---|---|
| `ServicioDocumentosTecnicos.cs` | Listar, guardar (con sus embeddings), descargar y borrar PDF. |
| `ServicioSugerencias.cs` | `BuscarAsync`: los pasos de la sugerencia, en orden. |
| `BaseVectorial.cs` | Conexión a PostgreSQL con pgvector y creación de las tablas `vectorial.*`. |
| `DocumentoPdf.cs` | Leer el PDF y cortarlo en fragmentos. También define las clases de datos compartidas (`FuenteDocumento`, `SeccionDocumento`, etc.). |
| `TextoDelManual.cs` | Limpiar el ruido del PDF y dividir el texto en secciones por sus títulos. |
| `AdvertenciasDelManual.cs` | Separar las advertencias del resto de la sección. |
| `SeccionesCandidatas.cs` | Unir secciones partidas, quitar repetidas y detectar opciones ambiguas. |
| `RedaccionDeSugerencia.cs` | Instrucciones para Qwen, guardia de números y formato final de la respuesta. |
| `ServicioOllama.cs` / `ServicioReranker.cs` | Llamadas a Ollama (bge-m3 y Qwen) y al reranker ONNX. |

Pasos:

1. **Recuperación:** la descripción se convierte en vector con `bge-m3`. Después se hacen dos búsquedas simples y se combinan en C#:
   - `BuscarIdsPorParecidoAsync`: los 30 fragmentos con el embedding más parecido (operador `<=>` de pgvector, distancia coseno).
   - `BuscarIdsPorPalabrasAsync`: los 30 fragmentos que mejor contienen las palabras de la consulta (búsqueda de texto de PostgreSQL en español).
   - `CombinarRankings`: cada fragmento suma `1 / (60 + posición)` por cada lista en la que aparece (fusión de rankings RRF). Así, un fragmento que está alto en las dos listas queda primero.
   - `LeerFragmentosCercanosAsync`: se descartan los que están demasiado lejos (`DistanciaMaximaPermitida`) y se quedan los 18 de mayor puntaje.
2. **Limpieza:** se quitan encabezados de página con número (por ejemplo `MANTENIMIENTO PROCEDIMIENTO DE MANTENIMIENTO 4-67`), marcadores de sección pegados a los títulos (`ACEITE4 ADVERTENCIA`) y viñetas mal extraídas (`l `). Los números técnicos (horas, litros, medidas) no se tocan.
3. **Secciones:** cada fragmento se corta en secciones usando los títulos en mayúsculas del propio PDF. Si una sección sigue en el fragmento siguiente, se une. Un título corto seguido de otro (por ejemplo "Mantenimiento cada 2000 horas") se pasa como contexto de la sección siguiente. Las secciones repetidas se descartan.
4. **Reranking:** `bge-reranker-v2-m3` lee juntos la consulta y cada sección, y le da un puntaje de qué tan bien la responde. Es más preciso que el embedding porque compara los dos textos a la vez. En `ServicioReranker`: `ArmarLotes` agrupa las secciones de a 4, `ArmarSecuencia` une los tokens de la consulta y de la sección con los separadores que espera el modelo, `CalcularPuntajes` arma los tensores y ejecuta el modelo ONNX, y `CompararDeMayorAMenorPuntaje` ordena el resultado. Si la mejor sección tiene puntaje negativo, responde `422` (`SIN_CONTEXTO_TECNICO`).
5. **Ambigüedad:** si otras secciones relevantes (a menos de 3 puntos de la mejor) contienen en su título las mismas palabras de la consulta que la mejor, la consulta no alcanza para distinguirlas y se listan como opciones en "Aclaración". La comparación usa raíces de palabras sin tildes, sin reglas ni palabras propias de un documento.
6. **Advertencias:** se copian literalmente del manual (bloques `ADVERTENCIA`, `PRECAUCIÓN`, `PELIGRO`, `AVISO`, `IMPORTANTE`), sin pasar por el modelo. Una advertencia termina en el siguiente paso numerado, en otra palabra de aviso o en un subtítulo en minúsculas del PDF (por ejemplo "Comprobación 4 …" o "Sustitución • …"); lo que sigue vuelve al cuerpo de la sección. Si una advertencia es larga, se corta al final de una oración.
7. **Resumen:** si al quitar las advertencias queda menos de `LargoMinimoDeSeccion` caracteres, se resume la sección completa, para que la respuesta nunca quede vacía. Qwen (`qwen2.5:7b`, temperatura 0) resume la sección elegida en 2 a 4 oraciones con instrucciones de no agregar nada. Si el resumen contiene un número que no está en el texto del manual, o si Ollama falla, se descarta y se usa un extracto limpio del manual.
8. **Formato:** el texto empieza directamente con la respuesta, sin título (si la sección tiene contexto del manual, como "Mantenimiento cada 2000 horas…", se copia literal en la primera línea). Después siguen `Advertencias:` (si hay), `Aclaración:` (si es ambigua) y `Fuente:` (documento, página y sección). No se agrega un aviso de revisión: por decisión del equipo, quien usa la pantalla ya sabe que se trata de una sugerencia. Esto se aparta del paso 9 de la Especificación de Requerimientos y de ENDPOINTS-BACKEND.md, que piden indicar que es información de apoyo.

### Qué significa cada constante

Los números que controlan el comportamiento tienen nombre y están al principio de cada clase. Para ajustar el sistema se cambia el valor de la constante, sin buscar números sueltos en el código.

| Archivo | Constante | Valor | Para qué sirve |
|---|---|---|---|
| `DocumentosTecnicosController` | `CincuentaMegabytesEnBytes` | `50 * 1024 * 1024` | Tamaño máximo del PDF que se acepta (50 MB expresados en bytes). |
| `DocumentoPdf` | `LargoMaximoDeFragmento` / `LargoMinimoDeFragmento` | 1400 / 700 | Tamaño en caracteres de cada fragmento que se guarda con su embedding. Se corta en un espacio, nunca antes de 700. |
| `DocumentoPdf` | `CaracteresRepetidosEntreFragmentos` | 200 | Cuánto texto repite un fragmento del anterior, para no cortar una idea a la mitad. |
| `TextoDelManual` | `LargoMaximoDeSeccionUsadaComoContexto` | 150 | Un título con menos texto que esto antes del siguiente título (por ejemplo "Mantenimiento cada 2000 horas") se usa como contexto de la sección siguiente. |
| `AdvertenciasDelManual` | `MaximoDeAdvertencias` / `LargoMaximoDeAdvertencia` | 4 / 350 | Cuántas advertencias se muestran y cuánto texto como máximo cada una. |
| `AdvertenciasDelManual` | `LargoMinimoDeVinetaDeAdvertencia` | 60 | Una viñeta más corta, o con ":", se considera un dato (capacidad, herramienta) y no una advertencia. |
| `SeccionesCandidatas` | `LetrasDeRaiz` | 5 | Cuántas letras se comparan para decidir si dos palabras son la misma ("cambio" y "cambiar" → "cambi"). |
| `ServicioDocumentosTecnicos` | `FragmentosPorPedidoDeEmbeddings` | 8 | Cuántos fragmentos se mandan juntos a Ollama al procesar un PDF. |
| `ServicioSugerencias` | `ResultadosPorTipoDeBusqueda` | 30 | Candidatos que trae cada búsqueda (vectorial y textual) antes de combinarlas. |
| `ServicioSugerencias` | `ConstanteDeFusionDeRankings` | 60 | Constante estándar de la fusión RRF: puntaje = 1 / (60 + posición). |
| `ServicioSugerencias` | `DistanciaMaximaPermitida` | 0.45 | Distancia coseno máxima: un fragmento más lejano se considera no relacionado. |
| `ServicioSugerencias` | `MaximoDeFragmentosRecuperados` | 18 | Fragmentos que pasan a la etapa de secciones. |
| `ServicioSugerencias` | `MargenDePuntajeParaSeccionesRelevantes` | 3 | Una sección es relevante si su puntaje del reranker está a menos de 3 puntos de la mejor. |
| `SeccionesCandidatas` | `LargoMinimoDeSeccion` | 80 | Secciones más cortas (índices, listas de títulos) se descartan. |
| `RedaccionDeSugerencia` | `LargoMaximoDeTextoParaQwen` | 3000 | Caracteres del manual que recibe Qwen como máximo. |
| `RedaccionDeSugerencia` | `LargoMinimoDeResumen` / `LargoMaximoDeResumen` | 20 / 1200 | Un resumen de Qwen fuera de ese largo se descarta. |
| `ServicioOllama` | `ModeloDeEmbeddings` / `ModeloDeGeneracion` | `bge-m3` / `qwen2.5:7b` | Modelos que se piden a Ollama. |
| `ServicioOllama` | `DimensionesDelEmbedding` | 1024 | Largo del vector que devuelve `bge-m3`; debe coincidir con `VECTOR(1024)` de la tabla. |
| `ServicioReranker` | `TokenDeInicio` / `TokenSeparador` | 0 / 2 | Tokens especiales que el modelo del reranker espera al inicio y entre la consulta y el texto. |
| `ServicioReranker` | `TokensMaximosPorPar` | 512 | Límite de tokens que acepta el reranker para consulta + texto. |
| `OrdenTrabajoMapeador` | `PrioridadBajaEnBase` / `Media` / `Alta` | 1 / 3 / 5 | Cómo se guardan en la base las prioridades `baja`, `media` y `alta` de la API. |

`numero` (1, 2, 3…) recorre las opciones: con una consulta ambigua, cada nueva solicitud devuelve la siguiente opción.

Requisito: Ollama debe tener `qwen2.5:7b` además de `bge-m3` (`ollama pull qwen2.5:7b`, unos 4,7 GB). En CPU, cada sugerencia tarda entre 25 segundos y 1 minuto; la primera, algo más, porque carga el modelo en memoria (unos 5 GB de RAM).

## Acceso a datos asíncrono y cancelación

Toda la cadena (controlador, DAO y EF Core) es `async/await` de punta a punta, sin `.Result` ni `.Wait()`.

- Los métodos de los DAO terminan en `Async` y reciben `CancellationToken cancelacion` como último parámetro (por ejemplo `ListarPaginaAsync`, `RegistrarAsync`). Usan `ToListAsync`, `FirstOrDefaultAsync`, `CountAsync`, `AnyAsync`, `SaveChangesAsync`, `ExecuteUpdateAsync` y `ExecuteSqlInterpolatedAsync`.
- Las acciones de los controladores reciben un `CancellationToken` como parámetro: ASP.NET lo enlaza con `HttpContext.RequestAborted` y se pasa a cada DAO. Los nombres de las acciones no llevan sufijo `Async` (MVC lo quita de las rutas y de `CreatedAtAction`).
- Las transacciones (`BeginTransactionAsync`, bloqueos `FOR UPDATE`, `pg_advisory_xact_lock`) se abren con `await using`: si el cliente cierra la conexión, la excepción `OperationCanceledException` sale de la transacción, que se deshace entera al liberarse, así que nunca queda un dato a medias.
- Esa excepción no se captura en el código de negocio. `ManejadorGlobalDeErrores` la ignora (también el error `57014` de PostgreSQL) cuando el cliente cerró la solicitud, y la auditoría la registra como `499` en vez de `500`.
- La prueba `CancelacionPruebas` cancela solicitudes de ingreso de stock a mitad de camino y comprueba que el stock coincide siempre con los movimientos registrados.

## Detener el backend

Detener temporalmente la configuración de producción:

```bash
docker compose stop
```

Iniciar nuevamente la configuración de producción:

```bash
docker compose start
```

Detener y eliminar los contenedores de producción sin borrar el volumen de PostgreSQL:

```bash
docker compose down
```

Detener y eliminar solamente el contenedor de desarrollo:

```bash
docker compose -f compose.dev.yml down
```

Este último comando no elimina `sotra-postgres-local` ni sus datos, porque la base utilizada en desarrollo se administra por separado.

## Diagnóstico de las sugerencias de IA

Si `POST /api/v1/ordenes-trabajo/sugerencia` responde `502`, mirar el campo `codigo`:

- `SERVICIO_IA_NO_DISPONIBLE`: Ollama no responde. Comprobar que corre y que `OLLAMA_BASE_URL` es alcanzable **desde el contenedor** del backend: en `compose.dev.yml`, `localhost` apunta al anfitrión mediante `extra_hosts`, así que Ollama debe escuchar en el equipo anfitrión (por defecto el puerto 11434, con los modelos `bge-m3` y `qwen2.5:7b`). `curl http://localhost:11434/api/tags` debe responder. El detalle queda en el log como advertencia.
- `SUGERENCIA_FALLIDA`: otro error en la búsqueda, el reranker o la generación; el log del backend trae la excepción (`LogError`) con el identificador de correlación.
- `422 SIN_CONTEXTO_TECNICO`: el servicio funciona, pero ningún documento técnico procesado cubre la tarea.

Las pruebas de integración apuntan `OLLAMA_BASE_URL` a un puerto cerrado para comprobar el `502` sin depender de Ollama.

## Pruebas unitarias (`Api.PruebasUnitarias`)

Proyecto NUnit aparte de `Api.Pruebas` (las de integración levantan Testcontainers para todo su espacio de nombres). No usa base de datos, Docker ni red, y tarda alrededor de un segundo:

```bash
dotnet test Api.PruebasUnitarias
```

Hoy son 271 pruebas, por módulo: validaciones (contraseña, nombre de persona, teléfono), mapeadores (incluida la `Version` de la orden: determinista y sensible al contenido), `EstadosStock`, transiciones y reglas de motivo/técnico de las órdenes, `GeneradorReportesCsv` (RFC 4180 e inyección de fórmulas), `GeneradorReportesPdf` (bytes `%PDF`), `HoraUruguay`, servicios de manuales (`DocumentoPdf`, `TextoDelManual`, `AdvertenciasDelManual`, `SeccionesCandidatas`, `RedaccionDeSugerencia`), `EntradaSinCaracteresNulos` (convertidor JSON y middleware), `CabecerasDeSeguridadMiddleware`, opciones de límite de solicitudes y de alertas, y `PaginaDto`.

## Pruebas de seguridad, de casos y de carga locales

Scripts en `../herramientas-plan/` que usan una base descartable (`sotra-casos-pg`, puerto 55432; nunca la base local con datos): `iniciar_base_casos.sh` la crea, `correr_casos_api.sh` levanta una copia de la API y ejecuta los casos de API exportados de Bugasura (solo lectura, no escribe en Bugasura), `fuzz_seguridad_api.py` prueba inyecciones, XSS, tipos, campos extra, cuerpos malformados, subidas hostiles, cabeceras y CORS en todos los endpoints, y `carga_api.py` simula 20 usuarios con `datos_volumen.sql` (30 000 órdenes). Resultados en `../RESULTADOS-PRUEBAS-LOCALES.md`.

Rendimiento: `OrdenTrabajoDAO.Filtrar` es la base compartida del listado y de los reportes; `ListarResumenes` proyecta solo los campos necesarios y `ListarRepuestosUsados` agrupa en la base. Los índices están en `database/cambios/2026-10-16-indices-rendimiento.sql` (aplicar a mano).

## Concurrencia en las órdenes de trabajo

`PATCH /ordenes-trabajo/{id}/estado` y `PATCH /ordenes-trabajo/{id}/tecnico` (`OrdenTrabajoDAO.CambiarEstado` y `AsignarTecnico`) abren una transacción, bloquean la fila de la orden con `SELECT ... FOR UPDATE` y vuelven a leerla antes de validar la transición. Así dos cambios simultáneos se ejecutan en serie. La prueba `CincoCambiosDeEstadoSimultaneosAlMismoDestinoSoloSeAplicanUnaVez` falla sin el bloqueo (cinco aplicaciones en lugar de una). `PUT /ordenes-trabajo/{id}` (`OrdenTrabajoDAO.Actualizar`, que ahora recibe los cambios como `CambiosDeOrden` y devuelve `null` si la orden no existe) usa el mismo bloqueo: el controlador ya no modifica la entidad antes de entrar a la transacción.

## Dashboard analítico

`AnaliticaController` expone `GET /api/v1/reportes/tendencias`, `/tiempos` (con duración, tiempo por etapa y cumplimiento de fechas previstas), `/reincidencias`, `/repuestos-usados` y `/alertas-maquinaria` (contratos en `Documentacion/Flujo de datos SOTRA.md`, sección 16). Todo se calcula en la base con `GROUP BY`, `percentile_cont` y funciones de ventana (`LAG` y `LEAD`) en `AnaliticaDAO`, con consultas SQL constantes y parámetros tipados (`@desde`, `@clienteId`, etc.): no se arma SQL con texto de la persona. Los umbrales de las alertas y los días de reincidencia se configuran con la sección `AlertasMaquinaria` (variables `AlertasMaquinaria__SinAsignarHoras`, `__AsignadaSinIniciarHoras`, `__PausadaHoras`, `__MaquinaDetenidaAltaHoras`, `__MaquinaDetenidaMediaHoras`, `__MaquinaDetenidaBajaHoras` y `__ReincidenciaDias`), validada al iniciar (`OpcionesDeAlertasDeMaquinaria`). Los valores por defecto son provisionales hasta acordarlos con el área de postventa.

## Auditoría de acciones

`AuditoriaDeAcciones` (`Api/Infraestructura/AuditoriaDeAcciones.cs`) es un filtro global que registra, con `ILogger` (Serilog), cada acción que cambia datos (`POST`, `PUT`, `PATCH` y `DELETE`): la acción, el controlador, el id del recurso de la ruta, el código HTTP del resultado y el correo de quien la pidió (`sin autenticar` mientras la seguridad esté apagada). Se registran también las acciones rechazadas (`4xx`) y las que fallan (`500`). No guarda el cuerpo de la solicitud ni datos personales. Cada línea lleva el `IdCorrelacion`, así que se puede seguir una acción de punta a punta. Ejemplo: `Auditoria PATCH CambiarEstado OrdenesTrabajo recurso 45 resultado 200 usuario sin autenticar`. Cubierto por `AuditoriaPruebas` y `SeguridadPruebas`.
