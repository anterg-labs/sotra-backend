# Backend de SOTRA

## Configurar PostgreSQL con el archivo `.env`

1. Copiar `.env.example` como `.env` en la raíz de `sotra-backend/`.
2. Configurar la conexión local sin publicar las credenciales:

```env
DB_HOST=localhost
DB_PORT=5432
DB_NAME=sotra
DB_USER=TU_USUARIO
DB_PASSWORD=TU_PASSWORD
FRONTEND_ORIGIN=http://localhost
```

Esta configuración permite ejecutar el backend directamente en Windows contra PostgreSQL publicado en el puerto estándar `5432`.

`compose.yml` conserva la configuración original de producción con PostgreSQL y el backend. La configuración de desarrollo está separada en `compose.dev.yml` y carga las variables desde `sotra-backend/.env`.

El archivo `.env` no se debe versionar. `.env.example` se conserva como plantilla sin credenciales reales.

## Ejecutar directamente en Windows

Comprobar la compilación:

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

### Desarrollo local

La configuración de desarrollo usa la base local publicada en `localhost:5432`. Si se utiliza `sotra-postgres-local`, debe iniciarse antes:

```bash
docker start sotra-postgres-local
docker compose -f compose.dev.yml up -d --build
```

La API queda disponible en `http://localhost:8081`. El código fuente está montado dentro del contenedor y `dotnet watch --no-hot-reload` reinicia la aplicación al guardar cambios. Así se evita que la recarga en caliente intente leer archivos temporales de recursos estáticos que no existen en este montaje de Windows. Esta configuración no modifica ni reemplaza el `Dockerfile` ni el `compose.yml` de producción.

Servicios expuestos al equipo:

- PostgreSQL: host `localhost` y puerto `5432`.
- Backend de producción: `http://localhost:8080`.
- Backend de desarrollo: `http://localhost:8081`.
- Keycloak de desarrollo: `http://localhost:9080` (consola en `http://localhost:9080/admin`).

Comprobar la conexión:

```bash
docker exec sotra-postgres-local pg_isready -U sotra -d sotra
curl http://localhost:8080/api/v1/roles
curl http://localhost:8081/api/v1/roles
```

## Keycloak (autenticación)

`compose.dev.yml` levanta Keycloak 26.7.4 en el contenedor `sotra-keycloak-dev`, en modo `start-dev` y publicado en el puerto `9080`. Guarda sus datos en la misma base de PostgreSQL que el backend (`DB_HOST`, `DB_PORT` y `DB_NAME`), dentro del esquema `keycloak` y con un usuario propio, `keycloak_user`, que no tiene acceso a `operativo` ni a `vectorial`.

La configuración del realm `sotra` está versionada en `keycloak/realm-sotra.json`: clientes, roles, el client scope de audiencia, la política de contraseñas y la protección contra fuerza bruta. Keycloak arranca con `--import-realm` y lo importa solamente si el realm todavía no existe; si ya existe, no lo modifica. El archivo es una exportación parcial: no incluye usuarios (salvo la cuenta de servicio de `sotra-backend-admin`), ni claves de firma, y los secretos de los clientes aparecen como `**********`.

### Primera puesta en marcha

1. Copiar las variables `KC_*` de `.env.example` al `.env` y completar `KC_DB_PASSWORD` y `KC_BOOTSTRAP_ADMIN_PASSWORD` con valores propios.
2. Crear en PostgreSQL el usuario y el esquema de Keycloak, usando la misma contraseña que `KC_DB_PASSWORD`:

```sql
CREATE ROLE keycloak_user WITH LOGIN PASSWORD 'TU_PASSWORD_KEYCLOAK_DB';
CREATE SCHEMA keycloak AUTHORIZATION keycloak_user;
ALTER ROLE keycloak_user SET search_path = keycloak;
```

3. Levantar Keycloak. La primera vez crea sus tablas e importa el realm `sotra`:

```bash
docker compose -f compose.dev.yml up -d keycloak
```

4. Entrar a `http://localhost:9080/admin` con `KC_BOOTSTRAP_ADMIN_USERNAME` y `KC_BOOTSTRAP_ADMIN_PASSWORD`, cambiar al realm `sotra` y generar el secreto propio en **Clients** → `sotra-backend-admin` → **Credentials** → **Regenerate**.

Comprobar que el realm responde:

```bash
curl http://localhost:9080/realms/sotra/.well-known/openid-configuration
```

### Contenido del realm `sotra`

| Cliente | Tipo | Uso |
|---|---|---|
| `sotra-frontend` | Público, *Standard flow* con PKCE `S256` | Login de la PWA. Redirecciones y orígenes permitidos: `http://localhost`. |
| `sotra-api` | Confidencial, sin flujos de login | Representa a la API. Los tokens llevan `aud: sotra-api` gracias al client scope `sotra-api-audience`. |
| `sotra-backend-admin` | Confidencial, cuenta de servicio | La API lo usa para crear y modificar usuarios en Keycloak. Solo tiene `manage-users` y `view-users` de `realm-management`. |
| `sotra-postman` | Público, *Direct access grants* | Solo para desarrollo: pedir tokens con usuario y contraseña desde Postman. No debe existir en producción. |

Roles del realm: `administrador`, `jefe_mantenimiento`, `tecnico` y `panolero`. Viajan en el token dentro de `realm_access.roles`.

El login usa el correo como nombre de usuario. La política de contraseñas exige al menos 8 caracteres, una mayúscula, un dígito y un carácter especial; no permite el correo ni el usuario como contraseña, ni repetir las últimas 3. Después de 5 intentos fallidos la cuenta se bloquea temporalmente (desde 1 minuto hasta 15). Una contraseña asignada como temporal obliga a cambiarla en el primer ingreso.

Pedir un token de prueba con `sotra-postman`:

```bash
curl -d client_id=sotra-postman -d grant_type=password -d username=CORREO_DEL_USUARIO -d password=PASSWORD_DEL_USUARIO http://localhost:9080/realms/sotra/protocol/openid-connect/token
```

### Actualizar `realm-sotra.json`

Si se cambia la configuración del realm desde la consola, hay que volver a exportarla para que el resto del equipo la reciba: **Realm settings** → **Action** → **Partial export**, con **Include groups and roles** e **Include clients** activados, y reemplazar `keycloak/realm-sotra.json` con el archivo descargado. Un integrante que ya tiene el realm creado no recibe los cambios al reiniciar, porque la importación se saltea si el realm existe; para aplicarlos debe hacerlos a mano en la consola o borrar el realm y reiniciar Keycloak.

## Contratos de modificación

Los endpoints `PUT` reciben todos los campos editables del recurso. No funcionan como modificaciones parciales.

En usuarios son obligatorios `nombre`, `apellido`, `correo`, `telefono`, `idsRol`, `estado` y `fechaIngreso`. `contrasena` puede omitirse para conservar la contraseña actual.

En activos, `estado` es opcional y acepta `activo` o `inactivo`. Si se omite, el estado no cambia; cualquier otro valor responde `400`. Permite reactivar un activo dado de baja. El cliente y el modelo de máquina deben estar activos.

En tipos de máquina, `nombre` es obligatorio. En modelos de máquina son obligatorios `nombre` y `tipoMaquinaId`; `descripcion` acepta texto o `null`. En los dos, `estado` es opcional y acepta `activo` o `inactivo`: si se omite, el estado no cambia, y cualquier otro valor responde `400`. Así se puede reactivar un tipo o un modelo dado de baja.

`PATCH /api/v1/clientes/{id}` acepta `{ "estado": "inactivo" }` para la baja lógica y `{ "estado": "activo" }` para reactivar (`ClienteDAO.ReactivarCliente`). Los `PATCH` de tipos, modelos, activos y repuestos siguen aceptando solamente `inactivo`.

### Campos opcionales de clientes

En `POST /api/v1/clientes` y `PUT /api/v1/clientes/{id}`, `razonSocial` y `nombreContacto` son obligatorios. `correo`, `telefono`, `direccion` y `observaciones` aceptan texto o `null`.

El `PUT` recibe los seis campos editables. Enviar `null` en uno de los cuatro campos opcionales elimina su valor anterior. El frontend debe enviar `null` en lugar de una cadena vacía cuando el campo no tenga valor.

## Mensajes de error

Los controladores responden los errores esperados (`400`, `409`, `422`) con un objeto que incluye `mensaje`, un texto en español pensado para el usuario. El frontend muestra ese `mensaje` tal como llega, así que no debe contener datos técnicos. Por eso las `ArgumentException` se crean solo con el texto, sin el nombre del parámetro: si se agrega, .NET suma al mensaje un texto como `(Parameter 'idCliente')`. Los errores automáticos de validación de ASP.NET no traen `mensaje` y el frontend muestra su texto genérico.

## Repuestos, órdenes y documentos técnicos

La API implementa los módulos restantes que consume el frontend:

- `/api/v1/repuestos`: `GET`, `GET /{id}`, `POST`, `PUT /{id}` y `PATCH /{id}`.
- `/api/v1/ordenes-trabajo`: `GET`, `GET /{id}`, `POST`, `PUT /{id}`, `PATCH /{id}/estado`, `PATCH /{id}/tecnico` y `POST /sugerencia`. El `GET` es paginado y acepta búsqueda y filtros (ver "Listados paginados").
- `/api/v1/documentos-tecnicos`: `GET`, `POST`, `GET /{id}/archivo` y `DELETE /{id}`.
- `/api/v1/reportes/indicadores`: `GET`.

Las órdenes usan las tablas existentes del esquema `operativo`. La fecha prevista se conserva en la observación del primer registro de `historial_estado_ot`, y las sugerencias se guardan en `historial_recomendaciones_ia`. El responsable (`id_usuario_responsable`) es el jefe de mantenimiento: hasta que exista el login, `OrdenTrabajoDAO.Crear` asigna el primer usuario activo con el rol activo "Jefe de mantenimiento" y responde `400` si no hay ninguno. `Actualizar` conserva el responsable de la orden.

Los documentos se guardan en la tabla `vectorial.documento_tecnico`, creada por la API cuando hace falta. Todo el módulo de documentos y sugerencias accede a PostgreSQL con SQL directo (Npgsql) y no con Entity Framework, porque las tablas `vectorial.*` no forman parte del modelo de EF y usan el tipo `vector` de pgvector. Por eso también la validación del activo asociado se hace con SQL sobre `operativo.activo`; el resto de los módulos (usuarios, clientes, activos, repuestos y órdenes) usa Entity Framework. El archivo se valida por su firma `%PDF-`; si se marca para sugerencias, PdfPig extrae el texto. Al procesar un documento para IA, el texto se divide en fragmentos y cada uno se guarda con su embedding de `bge-m3` en `vectorial.fragmentos` (pgvector).

### Versión del backend (`GET /api/v1/version`)

La versión está en `version.json`, en la raíz del repositorio, con formato `x.y.z`. `VersionController` la lee y responde `{ "version": "x.y.z" }`; el frontend la muestra en el pie de página.

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
- `GET /api/v1/tipos-maquina` (`FiltrosTiposMaquinaDto`): `buscar` (nombre) y `estado`.
- `GET /api/v1/documentos-tecnicos` (`FiltrosDocumentosTecnicosDto`): `buscar` (nombre del archivo o del activo) y `activoId`. Además existe `GET /api/v1/documentos-tecnicos/{id}`, que devuelve un documento sin el archivo (`404` si no existe).

- `GET /api/v1/usuarios` (`FiltrosUsuariosDto`): `buscar` (nombre, apellido o correo), `rolId`, `estado` y `direccionOrden` (`desc` invierte el orden, para ver primero los más nuevos).
- `GET /api/v1/clientes` (`FiltrosClientesDto`): `buscar` (razón social, contacto o correo), `estado` y `direccionOrden`.
- `GET /api/v1/repuestos` (`FiltrosRepuestosDto`): `buscar` (código, nombre, marca o descripción), `estado`, `ubicacion` (contiene) y `bajoStock=true` (cantidad menor o igual a la mínima).
- `GET /api/v1/movimientos-stock` (`FiltrosMovimientosStockDto`): `buscar` (código o nombre del repuesto, o motivo), `repuestoId`, `tipo`, `fechaDesde`, `fechaHasta`, `ubicacion` y `usuarioId`. Ordenados del más nuevo al más viejo.

`PaginaDto` también tiene `ErrorDePaginacion()` (el cuerpo del `400`) y `Contiene(texto, buscado)` (comparación sin distinguir mayúsculas), que usan todos los controladores paginados.

### Movimientos de stock (`/api/v1/movimientos-stock` y `POST /api/v1/repuestos/{id}/ingresos`)

RF004-02 y RF004-07. **Requiere ampliar la tabla `operativo.movimiento_stock`:** el SQL sugerido está en `SUGERENCIAS-SQL.md` (agrega `tipo`, `motivo`, `ubicacion`, `observaciones`, `stock_anterior` y `stock_posterior`, pasa `cantidad` a entero y deja `id_usuario` opcional). Hasta aplicarlo, estos endpoints responden `500`; el resto de repuestos funciona. Modelo `MovimientoStock` (actualizado), `MovimientoStockDAO`, `MovimientoStockMapeador` y `MovimientosStockController`.

- `POST /api/v1/repuestos/{id}/ingresos`: recibe `{ "cantidad", "fecha" (AAAA-MM-DD), "motivo", "ubicacion", "observaciones" }`. En una transacción suma la cantidad al repuesto y registra un movimiento `ingreso` con el stock anterior y posterior. Responde `201` con el movimiento. Errores: `404` si el repuesto no existe; `409` `REPUESTO_INACTIVO`; `400` `CANTIDAD_INVALIDA` (debe ser mayor a cero), `FECHA_INVALIDA` (vacía o futura) y `DATOS_OBLIGATORIOS` (motivo y ubicación). Si la fecha es hoy se guarda la hora actual; si es otro día, ese día a las 12:00 UTC.
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

### Descarga de reportes en PDF (`GET /api/v1/reportes/{tipoReporte}/pdf`)

RF007-02. Devuelve un archivo `application/pdf` llamado `reporte-{tipoReporte}.pdf`, generado con la librería QuestPDF (licencia Community, configurada al inicio de `Program.cs`). El diseño del documento está en `Api/Servicios/GeneradorReportesPdf.cs`: título, fecha de generación (hora de Uruguay), criterios aplicados, tablas y número de página.

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

Detener y eliminar solamente los contenedores de desarrollo (backend y Keycloak):

```bash
docker compose -f compose.dev.yml down
```

Este último comando no elimina `sotra-postgres-local` ni sus datos, porque la base utilizada en desarrollo se administra por separado. Tampoco borra la configuración de Keycloak, que queda guardada en el esquema `keycloak` de esa base.
