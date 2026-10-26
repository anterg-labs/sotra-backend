# Backend de SOTRA

> ## Para pasar a producción: aplicar los cambios de base de datos
>
> Los cambios de la base están en `database/cambios/*.sql`. El despliegue local (`docker compose up -d`) y el de staging (`05-staging-backend.yml`) los aplican solos con el servicio `db-datos`. El despliegue a producción (`03-production.yml`) solo actualiza el backend: no copia ni ejecuta esos archivos. Para pasar a producción hay que:
>
> 1. **Declarar `db-datos` en el `compose.production.yml` del VPS** (no está en este repositorio), igual que en `compose.yml`, con los volúmenes `./database/init:/datos:ro` y `./database/cambios:/cambios:ro`.
> 2. **Completar el job `deploy` de `03-production.yml`** como en `05-staging-backend.yml`, antes de actualizar el backend: `actions/checkout`, copiar por `scp` `database/init/{01-init.sql,02-vectorial.sql,cargar-datos.sh}` y `database/cambios/*.sql` al VPS, y ejecutar `docker compose ... up -d database && docker compose ... run --rm db-datos`. Solo si termina bien, `pull backend` y `up -d --no-deps backend`.
> 3. **Hacer un respaldo (`pg_dump`) de la base de producción** justo antes de ejecutar `db-datos`.
> 4. **Revisar la primera ejecución:** si la base no tiene la tabla `public.cambios_aplicados`, se aplicarán de una vez todos los archivos de `database/cambios/`, incluido `2026-10-17-terminos-documento-unico.sql`, que borra datos. Registrar antes en esa tabla los cambios que ya estén aplicados, y comprobar que no haya teléfonos duplicados con y sin cero.
> 5. **Probar el flujo en staging y recién entonces poner `ENABLE_VPS_DEPLOY=true`.** Verificar con `docker compose logs db-datos` y `SELECT archivo FROM public.cambios_aplicados`.

> ## ⚠️ Leer antes del próximo despliegue a staging
>
> El cargador de la base (`database/init/cargar-datos.sh`) **cambió**: ya no recrea una base que existe, aplica los cambios pendientes de `database/cambios/`. Antes de desplegar:
>
> 1. **Editar `compose.staging.yml` en el VPS** (no está en este repositorio) y agregar al servicio `db-datos` el volumen `./database/cambios:/cambios:ro`. Sin eso, el cargador avisa «no existe /cambios» y no aplica nada, y la base de staging conserva la columna `movimiento_stock.estado` que el backend nuevo ya no usa (los movimientos de stock fallarían).
> 2. **Saber qué hará la primera ejecución:** en una base creada con el cargador anterior (sin la tabla `public.cambios_aplicados`) aplica **los 8 archivos de `database/cambios/` una sola vez**. Uno de ellos, `2026-10-17-terminos-documento-unico.sql`, **borra el historial antiguo de términos y condiciones y sus aceptaciones**, conservando el texto vigente. Si no se quiere, registrarlo antes como aplicado: `INSERT INTO public.cambios_aplicados (archivo) VALUES ('2026-10-17-terminos-documento-unico.sql');`.
> 3. **Si un cambio falla**, el servicio `db-datos` termina con error y el backend no arranca hasta resolverlo (ver `docker compose logs db-datos`).
> 4. **Para recrear la base a propósito** (borra los datos): `RECREAR_BASE=1`.
>
> Detalle en [Carga automática de datos](#carga-automática-de-datos). Este aviso se retira cuando staging ya esté desplegado con el nuevo cargador.

## Requisitos

### Teléfonos de usuarios y clientes

El alta y la modificación de usuarios y de clientes aceptan 8 dígitos, o 9 si empiezan con cero (`99123456` y `099123456`). `TelefonoAttribute` valida el formato y el teléfono se guarda y devuelve exactamente como se ingresó. La unicidad compara ambas escrituras como el mismo número: en usuarios con una comprobación previa y un índice único, y en clientes con el índice único `UK_TELEFONO_CLIENTE_NORMALIZADO`. Se rechazan letras, espacios, prefijos internacionales, 7 dígitos, 9 sin cero inicial y 10 o más. Los índices se aplican a las bases existentes al desplegar (ver «Carga automática de datos»). El requisito y los casos de Bugasura se alinean con la decisión de Nicolás del 23/10/2026.

El alta informa `correoDeContrasenaEnviado` (`true`, `false` o ausente cuando no correspondía enviarlo). Si falla el reenvío, `POST /api/v1/usuarios/{id}/restablecer-contrasena` responde 502 con `codigo: CORREO_NO_ENVIADO`.

### Linux

Docker Engine con el plugin `docker compose`. El usuario debe pertenecer al grupo `docker` (`sudo usermod -aG docker $USER` y volver a iniciar sesión).

### Windows

Docker Desktop. Los comandos `docker compose` son los mismos; se pueden ejecutar desde PowerShell o Git Bash.

Los archivos de `database/init/` deben tener saltos de línea LF; `.gitattributes` lo fuerza al clonar.

## Configuración con el archivo `.env`

1. Copiar `.env.example` como `.env` en la raíz de `sotra-backend/`.
2. Completar los valores. El archivo `.env` no se debe versionar; `.env.example` es la plantilla sin credenciales reales.

| Variable | Uso |
|---|---|
| `DB_NAME`, `DB_USER`, `DB_PASSWORD` | Base de datos que crea el contenedor `sotra-db` la primera vez. |
| `POSTGRES_INITDB_ARGS` | `compose.yml` fija ICU `es-UY` para ordenar en español al crear un volumen nuevo. No cambia una base existente. |
| `DB_HOST`, `DB_PORT` | Conexión desde la máquina (pgAdmin, `dotnet run`). `DB_PORT` es también el puerto que publica `sotra-db`. Dentro de Docker, el backend y Keycloak usan `db:5432`. |
| `API_PORT` | Puerto donde se publica la API (`8081`). |
| `ASPNETCORE_ENVIRONMENT` | `Production` en producción; `Development` habilita `/openapi`. |
| `FRONTEND_ORIGIN` | Origen permitido por CORS. |
| `OLLAMA_BASE_URL` | Ollama, que corre en la máquina: `http://host.docker.internal:11434`. |
| `RERANKER_CARPETA` | Carpeta con `model_quantized.onnx` y `tokenizer.json`, relativa a `compose.yml`. Se monta en `/modelos`. |
| `KC_*` | Keycloak (ver "Keycloak"). |
| `AUTH_OBLIGATORIA` | `true` exige token de Keycloak en los endpoints protegidos; `false` los deja abiertos mientras el frontend integra el login. |
| `PROXIES_CONFIANZA` | Direcciones o redes CIDR de los proxies que reenvían `X-Forwarded-For` (por ejemplo Nginx), para que el límite de solicitudes use la IP de cada persona. |

Los listados ordenan las columnas de texto sin distinguir mayúsculas y con intercalación española `es-UY-x-icu`, antes de paginar. El orden numérico y de fechas conserva sus tipos. La base local creada antes de esta corrección sigue con su locale original aunque `compose.yml` cambie; el procedimiento para migrarla sin borrar datos está en [Migración del locale de PostgreSQL](database/MIGRACION-LOCALE-ES-UY.md).

## Ejecutar directamente en el equipo

Con el SDK de .NET instalado (Linux, macOS o Windows). Comprobar la compilación:

```bash
dotnet build
```

Comprobar Entity Framework:

```bash
dotnet ef dbcontext info --context SotraContext
```

Fuera de Docker, la API lee el `.env` (DotNetEnv) y se conecta a `DB_HOST:DB_PORT`.

## Levantar el sistema con Docker

Hay un único `compose.yml` y un único `Dockerfile`. `compose.yml` levanta cuatro servicios:

| Servicio | Contenedor | Puerto | Qué es |
|---|---|---|---|
| `db` | `sotra-db` | `DB_PORT` | PostgreSQL 18 con pgvector (`pgvector/pgvector:pg18-trixie`). Datos en el volumen `postgres_data`. |
| `db-datos` | `sotra-db-datos` | — | Se ejecuta una vez y termina: prepara el esquema de Keycloak y carga `01-init.sql` (ver "Carga automática de datos"). |
| `backend` | `sotra-backend` | `API_PORT` (8081) | La API, compilada en Release con el `Dockerfile`. |
| `keycloak` | `sotra-keycloak` | `KC_PORT` (9080) | Autenticación (ver "Keycloak"). |

Construir y levantar todo:

```bash
docker compose up -d --build
```

La API queda en `http://localhost:8081` y la consola de Keycloak en `http://localhost:9080/admin`. El backend y Keycloak esperan a que `db-datos` termine. El backend llega a Ollama por `host.docker.internal`, que `compose.yml` define también para Linux (`host-gateway`).

El backend de `compose.yml` se ejecuta compilado, sin recarga automática: después de cambiar código hay que reconstruirlo con `docker compose up -d --build backend`.

**Desarrollo con recarga en caliente (lo que se usa a diario):** `compose.dev.yml` es una capa sobre `compose.yml` que solo cambia el servicio `backend` (`Dockerfile.dev`, código montado y `dotnet watch`); base, datos, Keycloak y variables son los mismos. `compose.ia.yml` es otra capa opcional que agrega Ollama, el modelo de embeddings y el reranker en contenedores.

```bash
docker compose -f compose.yml -f compose.dev.yml -f compose.ia.yml up -d --build
```

Sin la IA en contenedores, omitir `-f compose.ia.yml` (la API usa `OLLAMA_BASE_URL` y `RERANKER_CARPETA` del `.env`). Después de cambiar código no hace falta reconstruir. Si Keycloak recién se importó, hay que copiar el secreto de `sotra-backend-admin` a `KEYCLOAK_ADMIN_SECRETO` (ver la sección de Keycloak). Las variables del entorno del contenedor tienen prioridad sobre el `.env` del repositorio. El `Dockerfile` restaura los paquetes NuGet en una capa aparte, así que la reconstrucción no los vuelve a descargar salvo que cambien.

#### Carga automática de datos

Antes de iniciar el backend, `compose.yml` ejecuta el servicio de una sola vez `db-datos` (`database/init/cargar-datos.sh`), que decide según el estado de la base:

- **Base nueva (el esquema `operativo` no tiene tablas):** carga `01-init.sql` completo (estructura y datos iniciales) y registra como aplicados todos los archivos de `database/cambios/`, porque el init ya los incluye.
- **Base existente:** **no la recrea ni borra datos.** Aplica, en orden alfabético, los archivos de `database/cambios/` que falten según la tabla `public.cambios_aplicados` y los registra. Cada archivo es idempotente. Si uno falla (por ejemplo, filas que incumplen una restricción nueva), el servicio termina con error y el backend no arranca hasta resolverlo. En una base creada con la versión anterior del cargador, que no tiene esa tabla, la primera ejecución aplica todos los cambios una vez.
- **Recrear a propósito:** con la variable `RECREAR_BASE=1` borra y recrea `operativo` con `01-init.sql`; los datos actuales se pierden. Solo para ambientes de prueba.
- **Esquema `vectorial`:** `02-vectorial.sql` crea solo su estructura vacía, y únicamente si el esquema no existe. Si ya existe, no se toca. Los PDF, fragmentos y embeddings no vienen en el SQL; se generan con la asistencia documental.

Los cambios de estructura nuevos se entregan en dos lugares a la vez: se incorporan a `01-init.sql` (bases nuevas) y se agrega un archivo idempotente en `database/cambios/` (bases existentes). El contenedor necesita `database/cambios` montado en `/cambios`; `compose.yml` ya lo hace y el flujo de staging copia esos archivos al servidor.

**Dump exportado.** En la raíz de este repositorio queda `sotra-dump.sql`, un dump de PostgreSQL (`pg_dump`, esquema `operativo`, estructura y datos) **sin PDF ni embeddings**: sin el esquema `vectorial` (documentos y vectores), sin el esquema `keycloak` y sin las tablas de control de la carga. Se genera a partir de una base descartable creada con `01-init.sql` y se regenera cada vez que cambia la base, sin tocar la base local con datos. Para restaurarlo en una base vacía: `psql -U USUARIO -d BASE -f sotra-dump.sql`.

**Datos de arranque.** `01-init.sql` trae la estructura completa (con los índices de rendimiento y las restricciones vigentes) y datos listos para usar y probar, sin PDF ni embeddings (el esquema `vectorial` se crea vacío con `02-vectorial.sql`): 14 personas usuarias con los 4 roles (entre ellas `admin.prueba`, `jefe.prueba`, `tecnico.prueba` y `panolero.prueba@sotra.test`, que coinciden con los usuarios de prueba del realm de Keycloak), 6 clientes, 7 tipos y 10 modelos de máquina, 14 activos, 14 repuestos en todos los estados de stock (normal, bajo, sin stock e inactivo), 26 movimientos de stock coherentes con las existencias, 14 órdenes de trabajo en todos los estados con su historial, 4 observaciones y 4 sugerencias de IA, y los términos y condiciones vigentes (aceptados por 5 personas). Los usuarios no tienen contraseña en SOTRA: el acceso lo resuelve Keycloak. Toda tabla o restricción nueva debe incorporarse también a `01-init.sql` para que quien levante el proyecto parta de una base al día.

Para agregar un cambio: editar `01-init.sql`, crear el SQL en `database/cambios/` y volver a ejecutar `docker compose up -d`. Para ver qué hizo la carga: `docker compose logs db-datos`.

La imagen de la base está fijada en la variante `trixie` (Debian 13). PostgreSQL ordena el texto con la glibc del sistema: si un volumen creado con una variante se abre con otra (por ejemplo `pg18`, que es Debian 12), PostgreSQL no permite crear bases nuevas. No hay que cambiar la variante sobre un volumen existente.

### Usuario y esquema de Keycloak

Antes de comparar huellas, `cargar-datos.sh` crea, si todavía no existen, el usuario `KC_DB_USERNAME` con la contraseña `KC_DB_PASSWORD` y el esquema `keycloak`, de su propiedad. Corre en cada `docker compose up` sin tocar nada si ya están, y Keycloak arranca recién cuando `db-datos` terminó. `.gitattributes` fuerza saltos de línea LF en los `.sh` y en `database/init/`, porque con CRLF fallan dentro del contenedor.

Comprobar que todo responde:

```bash
docker exec sotra-db pg_isready -U TU_USUARIO -d sotra
curl http://localhost:8081/health
curl http://localhost:8081/api/v1/roles
```

## Keycloak (autenticación)

`compose.yml` levanta Keycloak 26.7.4 en el contenedor `sotra-keycloak`, publicado en el puerto `KC_PORT` (9080). Por ahora corre en modo `start-dev` (HTTP y `localhost`); para producción hay que pasarlo a `start` con el dominio real y HTTPS. Guarda sus datos en la misma base que el backend, dentro del esquema `keycloak` y con un usuario propio (`KC_DB_USERNAME`, por defecto `keycloak_user`), que no tiene acceso a `operativo` ni a `vectorial`.

La configuración del realm `sotra` está versionada en `keycloak/realm-sotra.json`: clientes, roles, el client scope de audiencia, la política de contraseñas y la protección contra fuerza bruta. Keycloak arranca con `--import-realm` y lo importa solamente si el realm todavía no existe; si ya existe, no lo modifica. El archivo es una exportación parcial: no incluye usuarios (salvo la cuenta de servicio de `sotra-backend-admin`), ni claves de firma, y los secretos de los clientes aparecen como `**********`.

### Primera puesta en marcha

1. Copiar las variables `KC_*` de `.env.example` al `.env` y completarlas con valores propios del entorno.
   - `KC_DB_USERNAME` y `KC_DB_PASSWORD`: usuario de la base con el que se conecta Keycloak. `db-datos` (`cargar-datos.sh`) lo crea con esa contraseña si todavía no existe.
   - `KC_BOOTSTRAP_ADMIN_USERNAME` y `KC_BOOTSTRAP_ADMIN_PASSWORD`: usuario y contraseña del administrador temporal que Keycloak crea al arrancar por primera vez. Solo se usan esa vez, cuando el realm `master` no tiene ningún administrador; cambiarlos después en el `.env` no tiene efecto. Conviene anotarlos antes de levantar Keycloak.
   - `KC_SMTP_*`: la cuenta de Gmail que envía los correos (ver "Correos con Gmail").

   Para generar una contraseña segura:

   ```bash
   python -c "import secrets; print(secrets.token_urlsafe(24))"
   ```

2. Levantar el sistema (`docker compose up -d --build`). La primera vez, Keycloak crea sus tablas e importa el realm `sotra`.
3. Entrar a `http://localhost:9080/admin` con `KC_BOOTSTRAP_ADMIN_USERNAME` y `KC_BOOTSTRAP_ADMIN_PASSWORD`, cambiar al realm `sotra` y generar el secreto propio en **Clients** → `sotra-backend-admin` → **Credentials** → **Regenerate**.
4. Reemplazar el administrador temporal por uno definitivo, en el realm `master`:
   1. **Users** → **Create new user** con un usuario propio (por ejemplo `nombre.admin`), correo y **Email verified** activado.
   2. **Credentials** → **Set password** con una contraseña fuerte y **Temporary** desactivado.
   3. **Role mapping** → **Assign role** → **Filter by realm roles** → `admin`.
   4. Cerrar sesión y entrar con el usuario nuevo. Comprobar que ya no aparece el aviso de administrador temporal.
   5. Recién entonces borrar el temporal: **Users** → `admin` → **Action** → **Delete**. Si se borra antes de comprobar el nuevo y algo falla, no queda ningún administrador.

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

### Pantalla de login (tema `sotra`)

El login de Keycloak usa el tema propio `keycloak/themes/sotra/login`, con el mismo diseño que tenía la pantalla de login del frontend: logo de CVM, «Iniciar sesión», campos Usuario y Contraseña, «¿Olvidaste tu contraseña?» y, en pantallas anchas, la foto de maquinaria con el lema. Hereda las plantillas del tema `base` de Keycloak y reemplaza:

- `template.ftl`: la estructura común de todas las pantallas (login, recuperar y elegir contraseña, errores).
- `login.ftl` (la etiqueta «Usuario») y `login-reset-password.ftl` («Recuperar contraseña» con el orden del frontend).
- `messages/messages_es.properties`: los textos propios.
- `resources/`: estilos, logo, foto, ícono y la fuente Inter.

`compose.yml` monta la carpeta en `/opt/keycloak/themes/sotra` y `realm-sotra.json` lo activa con `"loginTheme": "sotra"`. En un realm que ya existía, la importación no lo aplica: elegir `sotra` en la consola de Keycloak (Realm settings › Themes › Login theme) o actualizar el realm con la API de administración. Con `start-dev` Keycloak no cachea los temas, así que un cambio en los archivos se ve al recargar la página. Si se cambia el logo o la foto en el frontend (`public/images`), copiarlos también a `resources/img`.

### Correos con Gmail

Para "¿Olvidaste tu contraseña?" (RF001-12) Keycloak envía correos por el SMTP de Gmail (`smtp.gmail.com`, puerto `587`, con StartTLS y autenticación). En `realm-sotra.json` el servidor, el puerto, el remitente, el usuario y la contraseña no están escritos: son las variables `${KC_SMTP_HOST}`, `${KC_SMTP_PORT}`, `${KC_SMTP_FROM}`, `${KC_SMTP_USER}` y `${KC_SMTP_PASSWORD}`, que Keycloak reemplaza con los valores del `.env` al importar el realm. Así la contraseña nunca queda en el repositorio. Como el resto del archivo, esto solo se aplica cuando el realm se crea; en un realm que ya existe, el correo se cambia en la consola, en **Realm settings** → **Email**.

Gmail no acepta la contraseña normal de la cuenta, sino una contraseña de aplicación:

1. Activar la verificación en dos pasos de la cuenta en `https://myaccount.google.com/security`.
2. Crear la contraseña de aplicación en `https://myaccount.google.com/apppasswords` (por ejemplo, con el nombre `SOTRA Keycloak`).
3. Copiar los 16 caracteres, sin espacios, en `KC_SMTP_PASSWORD`. `KC_SMTP_FROM` y `KC_SMTP_USER` deben ser la misma dirección de Gmail.

Gmail permite unos 500 correos por día.

> **Estado en el entorno de desarrollo actual (2026-10-23): el envío de correos NO está configurado.** `KC_SMTP_USER` y `KC_SMTP_PASSWORD` están vacíos en el `.env` porque la contraseña de aplicación de Google es de una persona y no se guarda ni se versiona; `KC_SMTP_HOST`, `KC_SMTP_PORT` y `KC_SMTP_FROM` ya tienen sus valores. Mientras tanto no funcionan:
>
> - el correo para elegir la contraseña al **crear un usuario** (el alta igual responde `201`; la API registra una advertencia y el usuario queda sin contraseña);
> - el botón **«Restablecer contraseña»** (`POST /usuarios/{id}/restablecer-contrasena`);
> - **«¿Olvidaste tu contraseña?»** del login.
>
> Para poder iniciar sesión sin correo, definir la contraseña a mano en la consola de Keycloak (`http://localhost:9080/admin`, realm `sotra` → Users → el usuario → Credentials → Set password, con *Temporary* desactivado). Los 4 usuarios de prueba (`admin.prueba`, `jefe.prueba`, `tecnico.prueba` y `panolero.prueba@sotra.test`) ya existen en el Keycloak local con una contraseña definida de este modo.
>
> Para habilitar los correos: generar la contraseña de aplicación como se explica arriba, completar `KC_SMTP_USER` (la misma dirección que `KC_SMTP_FROM`) y `KC_SMTP_PASSWORD` en el `.env`, y recrear Keycloak (`docker compose -f compose.yml -f compose.dev.yml -f compose.ia.yml up -d --force-recreate keycloak`). Si el realm ya existía, la configuración de correo importada no se vuelve a aplicar: cambiarla también en la consola (Realm settings → Email).

Para probarlo: crear en el realm `sotra` un usuario con un correo real, abrir `http://localhost:9080/realms/sotra/account`, elegir **¿Has olvidado tu contraseña?**, escribir ese correo y abrir el enlace que llega (puede caer en spam). El enlace vence a los 5 minutos.

Pedir un token de prueba con `sotra-postman`:

```bash
curl -d client_id=sotra-postman -d grant_type=password -d username=CORREO_DEL_USUARIO -d password=PASSWORD_DEL_USUARIO http://localhost:9080/realms/sotra/protocol/openid-connect/token
```

### Validación de tokens en la API

La API acepta los tokens de acceso que emite Keycloak para el realm `sotra` (`Microsoft.AspNetCore.Authentication.JwtBearer`, configurado en `Api/Seguridad/AutenticacionKeycloak.cs`). Un token es válido si:

- está firmado con una clave del realm (la API descarga las claves públicas de Keycloak y verifica la firma sin consultarlo en cada pedido);
- su emisor (`iss`) es `KEYCLOAK_EMISOR`;
- su audiencia (`aud`) incluye `KEYCLOAK_AUDIENCIA` (`sotra-api`);
- no venció (el token de acceso dura 5 minutos).

| Variable | Valor en Docker | Para qué |
|---|---|---|
| `KEYCLOAK_EMISOR` | `http://localhost:9080/realms/sotra` | Emisor que deben tener los tokens. Es la URL pública de Keycloak, la que ve el navegador. |
| `KEYCLOAK_METADATA` | `http://keycloak:8080/realms/sotra/.well-known/openid-configuration` | De dónde descarga la API los metadatos y las claves. Dentro de Docker usa la red interna; Keycloak responde con direcciones internas gracias a `KC_HOSTNAME_BACKCHANNEL_DYNAMIC`. |
| `KEYCLOAK_AUDIENCIA` | `sotra-api` | Audiencia exigida. |

`compose.yml` define las tres. Para correr la API fuera de Docker (`dotnet run`) se toman del `.env`, con `KEYCLOAK_METADATA` apuntando a `localhost:9080`.

Keycloak manda los roles del realm dentro de `realm_access.roles`; la API los convierte en roles de .NET, así que se pueden usar con `[Authorize(Roles = "tecnico")]`. El nombre de usuario (`User.Identity.Name`) es `preferred_username`, que en este realm es el correo.

`GET /api/v1/sesion` exige token y devuelve el usuario que inició sesión: `idKeycloak` (el `sub` del token), `correo`, `nombre` y `roles` (solo los de SOTRA). Sin token o con un token inválido responde `401`.

### Autorización por rol

Cada endpoint declara una política con `[Authorize(Policy = Politicas.X)]`. Las políticas y los roles que las cumplen están en un solo archivo, `Api/Seguridad/PoliticasDeAcceso.cs`; para cambiar quién puede hacer qué, se edita esa matriz. Cuando el permiso existe en `operativo.permiso`, la política usa el mismo nombre.

| Política | Endpoints | Roles |
|---|---|---|
| `usuario.listar` | `GET /usuarios` | administrador, jefe_mantenimiento |
| `usuario.consultar` | `GET /usuarios/{id}`, `GET /roles` | administrador, jefe_mantenimiento |
| `usuario.crear`, `usuario.editar` | `POST`, `PUT`, `PATCH /usuarios`, `POST /usuarios/{id}/restablecer-contrasena` | administrador, jefe_mantenimiento (ver abajo) |
| `cliente.consultar` | `GET /clientes` | todos |
| `cliente.gestionar` | `POST`, `PUT`, `PATCH /clientes` | administrador, jefe_mantenimiento |
| `activo.consultar` | `GET /activos` | todos |
| `activo.crear`, `activo.editar` | `POST`, `PUT`, `PATCH /activos` | administrador, jefe_mantenimiento |
| `modelo.consultar`, `tipo_maquina.consultar` | `GET /modelos-maquina`, `GET /tipos-maquina` | administrador, jefe_mantenimiento, tecnico |
| `modelo.crear`, `modelo.editar`, `tipo_maquina.gestionar` | `POST`, `PUT`, `PATCH` de modelos y tipos | administrador, jefe_mantenimiento |
| `ot.consultar` | `GET /ordenes-trabajo`, `GET` de evidencias y su archivo | todos |
| `ot.crear` | `POST /ordenes-trabajo`, `POST /ordenes-trabajo/sugerencia` | administrador, jefe_mantenimiento |
| `ot.editar` | `PUT /ordenes-trabajo/{id}` | administrador, jefe_mantenimiento |
| `ot.asignar_tecnico` | `PATCH /ordenes-trabajo/{id}/tecnico` | administrador, jefe_mantenimiento |
| `ot.registrar_avance` | `PATCH /ordenes-trabajo/{id}/estado`, `POST` de evidencias | administrador, jefe_mantenimiento, tecnico |
| `documento.consultar` | `GET /documentos-tecnicos` y su archivo | administrador, jefe_mantenimiento, tecnico |
| `documento.gestionar` | `POST`, `DELETE /documentos-tecnicos` | administrador, jefe_mantenimiento |
| `stock.consultar` | `GET /repuestos`, `GET /repuestos/alertas-stock`, `GET /movimientos-stock` | administrador, panolero |
| `repuesto.gestionar` | `POST`, `PUT`, `PATCH /repuestos`, `PATCH /repuestos/{id}/stock-minimo` | administrador, panolero |
| `stock.registrar_movimiento` | `POST /repuestos/{id}/ingresos`, `/egresos` y `/ajustes` | administrador, panolero |
| `reporte.consultar` | `GET /reportes/indicadores`, `GET /reportes/{tipo}/pdf` y `/csv`, y el dashboard analítico (`/reportes/tendencias`, `/tiempos`, `/reincidencias`, `/repuestos-usados`, `/alertas-maquinaria`) | administrador, jefe_mantenimiento |
| `terminos.administrar` | `POST /terminos` | administrador |

`GET /terminos/vigente`, el estado y la aceptación de los términos no tienen política propia: alcanza con una sesión iniciada.

Además de la matriz, dos reglas dependen de quién pide (`Api/Seguridad/AlcancePorRol.cs`):

- **El técnico solo trabaja sobre sus órdenes.** Si el usuario tiene el rol `tecnico` y ningún otro de gestión (`administrador`, `jefe_mantenimiento`, `panolero`), `GET /ordenes-trabajo` devuelve solo las órdenes que tiene asignadas (aunque pida otro `tecnicoId`), y el detalle, el cambio de estado y las evidencias de una orden ajena responden `403`.
- **El jefe no gestiona administradores.** El jefe de mantenimiento crea, modifica, da de baja y restablece la contraseña de usuarios, pero no puede asignar el rol Administrador ni tocar a un usuario que lo tiene: la API responde `403` con `codigo: "SOLO_ADMINISTRADOR"` y un `mensaje`.

`GET /version` y `/health` son públicos (`[AllowAnonymous]`). Cualquier otro endpoint sin política exige al menos una sesión iniciada (política *fallback*), así que un endpoint nuevo nunca queda abierto por olvido. Sin token la API responde `401`; con un token válido pero sin el rol necesario, `403`.

`AUTH_OBLIGATORIA` controla la protección. Con `true` (el valor por defecto si falta) rige la matriz. Con `false` los endpoints con política no exigen token, para poder usar el frontend mientras todavía no integra el login; la API lo avisa en el log al arrancar. `GET /sesion` exige token en los dos casos. En producción debe quedar en `true`.

```bash
curl -H "Authorization: Bearer TOKEN" http://localhost:8081/api/v1/sesion
```

### Gestión de usuarios con Keycloak

Los datos de cada usuario (nombre, apellido, correo, teléfono, fecha de ingreso, estado y roles) se guardan en `operativo.usuario`, que es la fuente principal. Lo referido al login (contraseña, cambio obligatorio de contraseña, bloqueo por intentos y sesiones) vive solo en Keycloak. `id_keycloak` vincula los dos. La API copia a Keycloak lo que necesita para el login: correo (que es el nombre de usuario), nombre, apellido, roles y si está habilitado. No hace falta tocar los usuarios desde la consola de Keycloak.

El teléfono de usuario y de cliente admite 8 dígitos o 9 con cero inicial. Se guarda y devuelve exactamente como se ingresó; `099123456` vuelve a verse con el cero en el listado, detalle y edición. Para evitar duplicados, `099123456` y `99123456` se comparan como el mismo número. Las bases nuevas incluyen el índice único comparable en `database/init/01-init.sql`; para una base existente, `db-datos` aplica solo en el despliegue `database/cambios/2026-10-23-usuario-telefono-con-formato-original.sql` y `database/cambios/2026-10-23-cliente-telefono-con-formato-original.sql`. Si hay teléfonos duplicados con y sin cero, el cambio falla a propósito y el backend no arranca hasta resolverlos.

`Api/Servicios/ServicioKeycloakUsuarios.cs` usa la API de administración de Keycloak con la cuenta de servicio de `sotra-backend-admin`, que solo tiene `manage-users` y `view-users`.

| Endpoint | En la base | En Keycloak |
|---|---|---|
| `POST /usuarios` | Crea el usuario. Ya no recibe contraseña. | Crea el usuario sin contraseña, con la acción "cambiar contraseña" pendiente, y le envía un correo con el enlace para elegirla (válido 3 días). Si ya existía un usuario con ese correo, lo vincula y no envía el correo. |
| `PUT /usuarios/{id}` | Actualiza los datos y los roles. | Copia roles, nombre, correo y estado. Si queda inactivo, se cierran sus sesiones. |
| `PATCH /usuarios/{id}` (`inactivo`) | Baja lógica. | Lo deshabilita (no puede iniciar sesión) y cierra sus sesiones. |
| `POST /usuarios/{id}/restablecer-contrasena` | — | Le envía el correo para elegir una contraseña nueva. Responde `204`; `409` (`USUARIO_SIN_ACCESO`) si no está en Keycloak y `409` (`USUARIO_INACTIVO`) si está dado de baja. |

Cada operación corre en una transacción de la base que solo se confirma si Keycloak también aceptó el cambio. Si en un alta falla la base después de crear el usuario en Keycloak, se borra de Keycloak. Si Keycloak no responde, la API devuelve `502` (`KEYCLOAK_NO_DISPONIBLE`). Si falla solo el envío del correo del alta, el usuario queda creado y el administrador puede reenviarlo con `restablecer-contrasena`.

Los roles de `operativo.rol` se traducen a los de Keycloak (Administrador → `administrador`, Jefe de mantenimiento → `jefe_mantenimiento`, Técnico → `tecnico`, Pañolero → `panolero`); los roles inactivos no se copian. Los usuarios cargados antes de esta integración no tienen `id_keycloak`: se modifican solo en la base y no pueden iniciar sesión.

Variables (en Docker las define `compose.yml`, salvo el secreto):

| Variable | Valor |
|---|---|
| `KEYCLOAK_ADMIN_SECRETO` | Secreto de `sotra-backend-admin` (realm `sotra` → **Clients** → `sotra-backend-admin` → **Credentials**). Va en el `.env`. |
| `KEYCLOAK_ADMIN_URL` | `http://keycloak:8080` en Docker. |
| `KEYCLOAK_REALM`, `KEYCLOAK_ADMIN_CLIENTE` | `sotra`, `sotra-backend-admin`. |
| `KEYCLOAK_CLIENTE_FRONTEND` | `sotra-frontend`: después de elegir la contraseña, el usuario vuelve a `FRONTEND_ORIGIN`. |

Si falta alguna, la API arranca igual y solo fallan las operaciones de usuarios, con `502`.

### Usuario actual

Cada usuario de SOTRA (`operativo.usuario`) se vincula con su usuario de Keycloak por la columna `id_keycloak` (el `sub` del token, única). `Api/Seguridad/UsuarioActual.cs` traduce el token de cada pedido al `id_usuario`:

1. Busca el usuario con ese `id_keycloak`.
2. Si no hay ninguno, busca por correo (sin distinguir mayúsculas) entre los usuarios todavía no vinculados y le guarda el `id_keycloak`. Así cada usuario queda vinculado en su primer ingreso, sin cargar nada a mano; después la vinculación ya no depende del correo.
3. Si tampoco hay coincidencia, el usuario de Keycloak no está registrado en SOTRA y no hay `id_usuario`.

Se usa para el responsable de las órdenes que se crean, el usuario de las evidencias y el de los movimientos de stock. `GET /api/v1/sesion` también devuelve el `idUsuario`.

### Actualizar `realm-sotra.json`

Si se cambia la configuración del realm desde la consola, hay que volver a exportarla para que quede versionada: **Realm settings** → **Action** → **Partial export**, con **Include groups and roles** e **Include clients** activados, y reemplazar `keycloak/realm-sotra.json` con el archivo descargado. La exportación trae el correo con los valores reales de quien exporta (su cuenta de Gmail); antes de subirla hay que volver a dejar el bloque `smtpServer` con las variables `${KC_SMTP_*}`. Un Keycloak que ya tiene el realm creado no recibe los cambios al reiniciar, porque la importación se saltea si el realm existe; hay que aplicarlos a mano en la consola o borrar el realm y reiniciar Keycloak.

## Contratos de modificación

Desde 1.0.48, asignar mediante `PATCH /api/v1/ordenes-trabajo/{id}/tecnico` al técnico actual responde `409 TECNICO_YA_ASIGNADO` sin agregar historial, también ante solicitudes concurrentes. Se permite X → Y → X. Usuarios incorpora `cantidadOrdenesAsignadas` y Clientes `cantidadOrdenesAsociadas`, de solo lectura, que excluyen órdenes cerradas y canceladas y son ordenables antes de paginar. Se calculan mediante consultas agrupadas por los identificadores de la página, sin cargar las órdenes. No requiere cambios de estructura ni SQL. El historial de verificaciones vive en `../sotra-test-automation/Estado de las pruebas locales.md`; las pruebas pendientes se centralizan en `../PRUEBAS-PENDIENTES.md`, fuera del backend.

La versión de aplicación 1.0.39 corresponde a la corrección del selector de clientes y activos del frontend en órdenes de trabajo. Conserva los contratos de consulta y la estructura de la base de datos; el comportamiento y sus verificaciones están en `../sotra-documentation/Flujo de datos SOTRA.md` y `sotra-test-automation/Estado de las pruebas locales.md`.

Los endpoints `PUT` exigen todos los campos editables, incluidos los opcionales con valor o `null`. Omitir uno responde `400` sin escribir; vaciar un opcional lo guarda como `NULL`; los obligatorios nulos o blancos se rechazan. Las acciones puntuales usan `PATCH` y conservan los demás datos. Un `PATCH` vacío no aplica `0` ni `false` por defecto. Contratos y excepciones: `../sotra-documentation/Actualizacion de formularios y tratamiento de nulos.md` (1.0.38). Las bases nuevas incorporan CHECK contra obligatorios vacíos. El incremental `database/cambios/2026-10-21-textos-obligatorios-no-vacios.sql` se aplicó a la base local por autorización del usuario del 2026-10-21: siete restricciones validadas, sin filas inválidas ni cambios de recuentos; no se aplicó a servidores. La suite de integración final pasa 380 de 380 sobre PostgreSQL descartable.

En usuarios son obligatorios `nombre`, `apellido`, `correo`, `telefono`, `idsRol`, `estado` y `fechaIngreso`. La API no recibe contraseñas: la contraseña vive solo en Keycloak (ver "Gestión de usuarios con Keycloak").

En activos se envían los ocho campos editables. Cliente, modelo, serie y estado son obligatorios; descripción, año, nombre y ubicación admiten `null` explícito para vaciarlos. Cliente y modelo deben estar activos.

En tipos se envían nombre y estado, ambos obligatorios. En modelos se envían nombre, tipoMaquinaId, descripción y estado; solo descripción admite `null`. `PUT` no acepta un cuerpo incompleto.

Los `PATCH` de estado de clientes, repuestos, tipos, modelos, activos y usuarios aceptan `activo` o `inactivo` y conservan los demás datos. `PUT` de repuestos no cambia cantidad: el stock solo cambia mediante movimientos.

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

La autenticación y los permisos los resuelve Keycloak (ver "Validación de tokens en la API" y "Autorización por rol"). `/health`, `/health/ready` y `/api/v1/version` son públicos.

## Errores de negocio centralizados

Los controladores ya no repiten bloques `catch`. Las reglas de negocio lanzan excepciones tipadas (`ValidacionDeNegocioException` con su `Codigo`, `ConflictoDeDatosException`, `ErrorDeOrdenException`) y las violaciones de restricciones únicas de PostgreSQL llegan como `DbUpdateException`; `Api/Infraestructura/TraduccionDeErroresDeNegocio.cs` decide estado HTTP, `codigo` y texto en un único lugar (tabla por restricción y, si falta, por tabla) y `ManejadorGlobalDeErrores` lo responde como `ProblemDetails` con `codigo` y `mensaje`. Para agregar un duplicado nuevo basta una fila en esa tabla. Pruebas: `Api.PruebasUnitarias/TraduccionDeErroresPruebas.cs`. Los `catch` que quedan (documentos técnicos y sugerencias de IA) distinguen cancelación, servicio de IA caído y error inesperado, y dejan pasar las excepciones de negocio.

## Términos y condiciones

Un único documento editable, sin historial. `GET /api/v1/terminos/vigente` devuelve el texto y su marcador interno de cambios. `POST /api/v1/terminos` recibe únicamente `{ texto }` y responde `200`, modificando el mismo registro; valida de 20 a 20 000 caracteres. Si cambia el texto se incrementa el marcador y se reinician sus aceptaciones. Guardar sin cambios las conserva. No hay endpoint de historial, ni campos de título, datos o autor en el contrato.

La aceptación por persona exige `{ usuarioId, terminosId, version, acepta: true }`; es idempotente y rechaza contenido desactualizado con `409 TERMINOS_DESACTUALIZADOS`. Guardado y aceptación se serializan con bloqueo transaccional de PostgreSQL. La auditoría existente obtiene al autor de `IUsuarioActual` (el correo del token de Keycloak), sin permitir que el formulario lo elija; sin token (`AUTH_OBLIGATORIA=false`) registra «sin autenticar». El frontend usa un único editor visual y comparte el mismo modal para la vista previa y el footer; temporalmente recuerda la aceptación por navegador.

No se agregan tablas. Las bases nuevas exigen un único registro mediante `CHECK (vigente)` y el índice único parcial. Para bases existentes se entrega `database/cambios/2026-10-17-terminos-documento-unico.sql`, que elimina el historial antiguo y conserva el texto vigente. **No aplicado a la base local ni al VPS: requiere ejecución manual autorizada.** No se reutilizan las columnas heredadas de título, datos separados o autor en el contrato. Detalles en `Documentacion/Flujo de datos SOTRA.md`, sección 15 bis.

## Modelos de IA y base de datos local

Todo lo que necesita el backend vive dentro de `sotra-backend/`; no hay carpetas auxiliares en la raíz del trabajo.

- **Base de datos:** `docker compose up -d db` (o `docker compose up -d` para base, datos iniciales y API) usa `compose.yml`, el esquema de `database/init/` y las variables del `.env`. Los cambios incrementales están en `database/cambios/` y se aplican a mano.
- **Modelos de IA:** `compose.ia.yml` define el volumen `sotra_modelos_reranker` (reranker ONNX y tokenizador, ~560 MB) y `sotra_modelos_ollama` (modelos de Ollama). El servicio `reranker-modelo` los descarga la primera vez, el backend los monta en `/modelos` (solo lectura) y `.env.example` ya apunta a `RERANKER_MODELO_RUTA=/modelos/model_quantized.onnx`.
- Para ver o reponer el volumen: `docker volume inspect sotra_modelos_reranker`; si se borra, `docker compose -f compose.yml -f compose.ia.yml up -d` lo vuelve a llenar.

## Orden de los listados

Cada listado paginado define su lista cerrada de columnas ordenables: `ColumnasOrdenables<T>` (`SOTRA-BACKEND/Operaciones/ColumnasOrdenables.cs`) en cada DAO de EF Core y un diccionario de expresiones SQL fijas en `ServicioDocumentosTecnicos`. `ordenarPor` y `direccionOrden` solo se buscan en esa lista; lo que no está responde `400 ORDEN_INVALIDO` y nunca llega a la consulta. Siempre se desempata por el identificador. Pruebas: `Api.Pruebas/Caracterizacion/OrdenDeListadosPruebas.cs` (cada columna en ambos sentidos, columna inválida, texto con SQL y paginación estable).

## Formato de los errores

Los errores explícitos de los controladores se crean con `this.RespuestaDeError(estado, codigo, mensaje)` (`Api/Infraestructura/RespuestasDeError.cs`): `ProblemDetails` con `codigo` estable, `mensaje` e `idCorrelacion`, igual que el manejo global. No se devuelven objetos anónimos como error. Prueba: `Api.Pruebas/Caracterizacion/FormatoDeErroresPruebas.cs`.

## Resiliencia de Ollama

`ServicioOllama` recibe un `HttpClient` de `IHttpClientFactory` (`Api/Infraestructura/ConfiguracionDeOllama.cs`) con `Microsoft.Extensions.Http.Resilience` 10.11.0 (MIT, mantenido por Microsoft; agrega Polly 8 como dependencia). Reintenta hasta 2 veces con retroceso exponencial solo los fallos transitorios (error de conexión, 408, 429 y 5xx; un 4xx no se reintenta), tiene un límite de 5 minutos por intento y 10 en total, y corta el circuito 30 segundos cuando fallan la mitad de las solicitudes de una muestra de al menos 5. Con el circuito abierto la API responde `502 SERVICIO_IA_NO_DISPONIBLE` de inmediato en vez de esperar. `ServicioReranker` no usa HTTP (ONNX local), así que no lleva esta política. Pruebas: `Api.PruebasUnitarias/ClienteOllamaPruebas.cs`.

## Observabilidad (OpenTelemetry)

Trazas y métricas con OpenTelemetry, **apagadas por defecto**: no cambian nada hasta definir un destino. Se activan con `OTEL_EXPORTER_OTLP_ENDPOINT` (por ejemplo el receptor OTLP de Grafana Alloy del VPS, `http://127.0.0.1:4318` con `OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf`) o con `Observabilidad__Habilitada=true`; se apagan con `Observabilidad__Habilitada=false`. El destino, los encabezados de autenticación, el muestreo y el nombre del servicio (`OTEL_SERVICE_NAME`, por defecto `sotra-api`) usan las variables estándar `OTEL_*`. Código en `Api/Infraestructura/Observabilidad.cs`.

- **Trazas:** cada solicitud HTTP entrante (sin `/health`), las llamadas salientes (`HttpClient`, Ollama) y cada comando a PostgreSQL (fuente `Npgsql`).
- **Métricas estándar:** ASP.NET Core (solicitudes, duración, errores), `HttpClient`, tiempo de ejecución .NET (GC, hilos, memoria), Npgsql (conexiones, comandos) y EF Core.
- **Métricas de SOTRA** (medidor `Sotra.Api`): `sotra.acciones` (altas, modificaciones, bajas, movimientos y cambios de estado, por recurso, acción y clase de resultado), `sotra.sugerencias_ia.duracion` (histograma en segundos, por resultado `ok`, `sin_contexto`, `no_disponible` o `error`), `sotra.reportes_generados` (por tipo y formato) y `sotra.entradas_rechazadas`. Además, `RecolectorDeMetricasDeNegocio` (solo si la observabilidad está habilitada) mide cada 60 segundos, con un límite de 10 segundos, medidores observables: `sotra.ordenes.por_estado` (etiqueta `estado`), `sotra.ordenes.horas_hasta_el_cierre` (promedio de los últimos 7 días, en horas), `sotra.repuestos.con_stock_bajo` y `sotra.repuestos.sin_stock`. Si una medición falla, conserva la anterior y registra una advertencia. Prueba: `Api.Pruebas/Caracterizacion/MetricasDeNegocioPruebas.cs`.
- **Logs:** cada línea lleva `IdCorrelacion` y `TraceId`, para pasar de un log a su traza.
- Se comprobó contra un colector OTLP local: llegan trazas con spans HTTP y de base de datos, y métricas con `sotra.acciones`. Las pruebas unitarias están en `Api.PruebasUnitarias/ObservabilidadPruebas.cs`.

## Logs

El backend usa Serilog. En desarrollo escribe texto legible en la consola; en los demás ambientes escribe JSON compacto (una línea por evento), que Docker recoge. Los niveles se ajustan en la sección `Serilog` de `appsettings.json`. Cada solicitud se registra con método, ruta (sin parámetros de consulta), estado, duración y dirección IP, y lleva un `IdCorrelacion` que también se devuelve en la cabecera `X-Correlation-Id`. No se registran contraseñas, tokens ni el contenido de los archivos.

## Repuestos, órdenes y documentos técnicos

La API implementa los módulos restantes que consume el frontend:

- `/api/v1/repuestos`: `GET`, `GET /alertas-stock`, `GET /{id}`, `POST`, `PUT /{id}`, `PATCH /{id}`, `PATCH /{id}/stock-minimo` y `POST /{id}/ingresos`, `/egresos` y `/ajustes`.
- `/api/v1/ordenes-trabajo`: `GET`, `GET /{id}`, `POST`, `PUT /{id}`, `PATCH /{id}/estado`, `PATCH /{id}/tecnico` y `POST /sugerencia`. El `GET` es paginado y acepta búsqueda y filtros (ver "Listados paginados").
- `/api/v1/documentos-tecnicos`: `GET`, `POST`, `PUT /{id}`, `PATCH /{id}/vigencia`, `GET /{id}/archivo` y `DELETE /{id}`.
- `/api/v1/reportes/indicadores`: `GET`.

Las órdenes usan las tablas existentes del esquema `operativo`. La fecha prevista se conserva en la observación del primer registro de `historial_estado_ot`, y las sugerencias se guardan en `historial_recomendaciones_ia`. El responsable (`id_usuario_responsable`) es el usuario que crea la orden (ver "Usuario actual"). Si el pedido no trae token (con `AUTH_OBLIGATORIA=false`) o el usuario de Keycloak no está registrado en SOTRA, `OrdenTrabajoDAO.Crear` asigna el primer usuario activo con el rol activo "Jefe de mantenimiento" y responde `400` si no hay ninguno. `Actualizar` conserva el responsable de la orden.

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
- `GET /api/v1/documentos-tecnicos` (`FiltrosDocumentosTecnicosDto`): `buscar` (título, descripción, nombre del archivo o del activo), `activoId`, `modeloMaquinaId`, `tipo` (`normativa`, `procedimiento_interno`, `manual_tecnico`, `politica_seguridad` u `otro`) y `vigente`. Los documentos obsoletos (`vigente = false`) no se usan para las sugerencias de IA. Los campos nuevos (`titulo`, `tipo`, `descripcion`, `vigente` y `modelo_maquina_id`) se agregan a una base existente aplicando a mano `database/cambios/2026-10-20-documentos-datos-generales.sql`; las bases nuevas ya los traen en `database/init/02-vectorial.sql`. Además existe `GET /api/v1/documentos-tecnicos/{id}`, que devuelve un documento sin el archivo (`404` si no existe).

- `GET /api/v1/usuarios` (`FiltrosUsuariosDto`): `buscar` (nombre, apellido o correo), `rolId`, `estado` y `direccionOrden` (`desc` invierte el orden, para ver primero los más nuevos).
- `GET /api/v1/clientes` (`FiltrosClientesDto`): `buscar` (razón social, contacto o correo), `estado` y `direccionOrden`.
- `GET /api/v1/repuestos` (`FiltrosRepuestosDto`): `buscar` (código, nombre, marca o descripción), `estado`, `ubicacion` (contiene) `bajoStock=true` (stock bajo o sin stock), `estadoStock` (`normal`, `bajo` o `sin_stock`) y `orden=criticidad`. Cada repuesto trae `estadoStock`, calculado por `EstadosStock.Calcular` (sin stock con cantidad 0; bajo si el mínimo es mayor que 0 y la cantidad lo alcanza).
- `POST` y `PUT /api/v1/repuestos`: si el código ya existe, responden `409` con `codigo: "CODIGO_REPUESTO_DUPLICADO"` y un mensaje legible. El frontend sitúa el error junto al campo Código.
- `GET /api/v1/movimientos-stock` (`FiltrosMovimientosStockDto`): `buscar` (código o nombre del repuesto, o motivo), `repuestoId`, `tipo`, `fechaDesde`, `fechaHasta`, `ubicacion` y `usuarioId`. Ordenados del más nuevo al más viejo.

`PaginaDto` también tiene `ErrorDePaginacion()` (el cuerpo del `400`) y `Contiene(texto, buscado)` (comparación sin distinguir mayúsculas), que usan todos los controladores paginados.

### Movimientos de stock (`/api/v1/movimientos-stock` y `POST /api/v1/repuestos/{id}/ingresos`, `/egresos` y `/ajustes`)

RF004-02 y RF004-07. **Requiere ampliar la tabla `operativo.movimiento_stock`:** el SQL sugerido está en `SUGERENCIAS-SQL.md` (agrega `tipo`, `motivo`, `ubicacion`, `observaciones`, `stock_anterior` y `stock_posterior`, pasa `cantidad` a entero y deja `id_usuario` opcional). Hasta aplicarlo, estos endpoints responden `500`; el resto de repuestos funciona. Modelo `MovimientoStock` (actualizado), `MovimientoStockDAO`, `MovimientoStockMapeador` y `MovimientosStockController`.

- `POST /api/v1/repuestos/{id}/ingresos`: recibe `{ "cantidad", "fecha" (AAAA-MM-DD), "motivo", "ubicacion", "observaciones" }`. Mediante `MovimientoStockDAO.Registrar`, en una transacción que bloquea la fila del repuesto (`SELECT ... FOR UPDATE`), suma la cantidad y registra un movimiento `ingreso` con el stock anterior y posterior. Responde `201` con el movimiento. Errores: `404` si el repuesto no existe; `409` `REPUESTO_INACTIVO`; `400` `CANTIDAD_INVALIDA` (debe ser mayor a cero), `FECHA_INVALIDA` (vacía o futura) y `DATOS_OBLIGATORIOS` (motivo y ubicación). Si la fecha es hoy se guarda la hora actual; si es otro día, ese día a las 12:00 UTC.
- `POST /api/v1/repuestos/{id}/egresos` (`cantidad`, `motivo`, `ubicacion?`, `observaciones?`) y `POST /api/v1/repuestos/{id}/ajustes` (`cantidadReal`, `motivo`, `observaciones?`) usan el mismo método y bloqueo. `409` `STOCK_INSUFICIENTE` si el egreso supera el stock, `409` `REPUESTO_INACTIVO`, `400` `CANTIDAD_INVALIDA` y `DATOS_OBLIGATORIOS`. En un ajuste la `cantidad` del movimiento es la diferencia con signo.
- `PATCH /api/v1/repuestos/{id}/stock-minimo` cambia solo `cantidadMinima` (entero mayor o igual a 0). `GET /api/v1/repuestos/alertas-stock` devuelve `{ bajo, sinStock, total }` de repuestos activos, contados en la base.
- Restricción de base: `database/cambios/2026-10-16-repuesto-stock-no-negativo.sql` (aplicar a mano) impide `cantidad` y `cant_min` negativos; `01-init.sql` ya la incluye para bases nuevas.
- Los tres exigen la política `stock.registrar_movimiento` (administrador y pañolero) y registran al usuario que hizo el movimiento.
- `GET /api/v1/movimientos-stock` (paginado, ver arriba) y `GET /api/v1/movimientos-stock/{id}`: cada movimiento tiene `id`, `repuestoId`, `codigoRepuesto`, `nombreRepuesto`, `tipo`, `cantidad`, `fecha`, `ubicacion`, `usuarioId`, `nombreUsuario`, `motivo`, `observaciones`, `stockAnterior` y `stockPosterior`.

La especificación también pide filtrar repuestos por "categoría", pero el repuesto no tiene ese dato en la base; queda pendiente de definir.

### Catálogo de estados (`GET /api/v1/ordenes-trabajo/estados`)

Los estados de las órdenes, sus etiquetas y sus reglas salen de la base: `operativo.estado_ot` (`nombre` es el valor, `descripcion` la etiqueta y `orden`, `fase`, `exige_tecnico`, `completa_fecha`, `tono`, `codigo_alerta` y `es_inicial` el comportamiento) y `operativo.transicion_estado_ot` (cada cambio permitido, con `exige_motivo`, `puede_tecnico`, `accion`, `principal` y `al_asignar_tecnico`). El código no nombra ningún estado: `ReglasDeEstadoOrden` solo aplica lo que dice la base. El endpoint los devuelve en el orden del ciclo de vida, `[{ "valor": "en_ejecucion", "etiqueta": "En ejecución", "orden": 3, "fase": "en_curso", "tono": "info" }, …]`, solo los activos; si una fila no tiene etiqueta, se arma una con el nombre (`En ejecucion`). Lo consumen el filtro del listado, los reportes, los formularios y las etiquetas del frontend, que no tienen listas propias. Una prueba de integración comprueba que la base tenga exactamente el ciclo de vida del documento (sección 13.1 de `Flujo de datos SOTRA.md`) y las pruebas unitarias recorren las 196 combinaciones de estado, técnico y motivo. Los siete estados vienen en `01-init.sql` y, para bases existentes, en `database/cambios/2026-10-25-estado-ot-catalogo-completo.sql` (agrega `cancelada` y las etiquetas), que el despliegue aplica solo.

### Ciclo de vida de la orden (`PATCH /api/v1/ordenes-trabajo/{id}/estado` y `/tecnico`)

RF005-06 y RF005-07, según el ciclo de vida del anteproyecto. Las transiciones permitidas están en la tabla `transicion_estado_ot` y se validan tanto en `PATCH /estado` como en `PUT`:

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
- Errores con `codigo`: `400` `TRANSICION_INVALIDA`, `TECNICO_OBLIGATORIO` o `MOTIVO_OBLIGATORIO`; `403` `TRANSICION_NO_PERMITIDA` (un técnico pide un cambio que no es suyo, como cerrar); `409` `ORDEN_CERRADA` (cambiar una orden cerrada) u `ORDEN_NO_MODIFICABLE` (asignar técnico a una orden cerrada o cancelada); `404` si la orden no existe.
- Ediciones simultáneas: `OrdenTrabajoDto.Version` (hash SHA-256 truncado del DTO, calculado en `OrdenTrabajoMapeador`) se devuelve también como `ETag` en `GET /ordenes-trabajo/{id}` y `PUT`. El `PUT` acepta `If-Match` y lo compara dentro del bloqueo de fila de `OrdenTrabajoDAO.Actualizar`; si no coincide responde `412` `ORDEN_MODIFICADA` sin cambiar nada. Sin el encabezado (o con `*`) no se verifica. CORS permite `If-Match` y expone `ETag`.
- `OrdenTrabajoDto` incluye `transiciones` (los cambios posibles desde el estado actual, con `exigeMotivo`, `puedeTecnico`, `accion` y `principal`) y `historialEstados` (`estado`, `fechaHora` en UTC y `observacion`, del más viejo al más nuevo; la observación del primer registro no se muestra porque guarda la fecha prevista).

El historial todavía no registra el usuario responsable del cambio: `historial_estado_ot` no tiene una columna para eso.

### Evidencias (`/api/v1/ordenes-trabajo/{id}/evidencias`)

RF005-05. **Requiere la tabla `operativo.evidencia_ot`, que todavía no existe:** el SQL sugerido está en `SUGERENCIAS-SQL.md` (raíz del directorio de trabajo) y hay que aplicarlo antes de usar estos endpoints. Modelo `EvidenciaOt`, mapeo en `SotraContext`, `EvidenciaOtDAO` y `EvidenciasController`.

- `GET`: lista las evidencias de la orden, sin el contenido del archivo. Cada una tiene `id`, `ordenTrabajoId`, `tipo`, `observacion`, `fechaRegistro` (`AAAA-MM-DD`), `creadoEn` (UTC), `nombreArchivo`, `tipoContenido` y `tieneArchivo`.
- `POST` (`multipart/form-data`): campos `tipo` (`foto`, `documento` u `observacion`), `observacion`, `fechaRegistro` y `archivo`. Responde `201`. Reglas: la orden no puede estar cerrada ni cancelada (`409` `ORDEN_NO_MODIFICABLE`); `foto` y `documento` exigen archivo (`ARCHIVO_OBLIGATORIO`) y `observacion` exige texto (`OBSERVACION_OBLIGATORIA`); la fecha no puede ser futura (`FECHA_REGISTRO_INVALIDA`); tipo desconocido: `TIPO_EVIDENCIA_INVALIDO`. El archivo se valida por su contenido real (firma de JPG, PNG o PDF), no por la extensión: las fotos solo JPG o PNG y los documentos PDF, JPG o PNG (`ARCHIVO_NO_PERMITIDO`); hasta 10 MB (`413` `ARCHIVO_DEMASIADO_GRANDE`).
- `GET /{evidenciaId}/archivo`: descarga el archivo con su tipo de contenido.

El usuario que registra la evidencia (`id_usuario`) es el del token (ver "Usuario actual"); queda vacío si el pedido no trae token o el usuario no está registrado en SOTRA.

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

RF007-02. Devuelve un archivo `application/pdf` llamado `reporte-{tipoReporte}.pdf`, generado con la librería QuestPDF (el encabezado lleva el logo de CVM incrustado desde `Api/Recursos/logo-cvm.png` y los tonos provisorios de la marca; licencia Community, configurada al inicio de `Program.cs`). El diseño del documento está en `Api/Servicios/GeneradorReportesPdf.cs`: título, fecha de generación (hora de Uruguay), criterios aplicados, tablas y número de página. `GET /api/v1/reportes/{tipoReporte}/csv` devuelve las mismas tablas (se arman una sola vez en `ReportesController.ArmarReporte`) como `text/csv` UTF-8 con BOM y separador `;`; `Api/Servicios/GeneradorReportesCsv.cs` aplica las comillas de RFC 4180 y antepone `'` a las celdas que empiezan con `=`, `+`, `-`, `@`, tabulación o retorno de carro (inyección de fórmulas).

Tipos de reporte (`tipoReporte`):

| Valor | Contenido |
|---|---|
| `ordenes-trabajo` | Una fila por orden: número, fecha de creación, cliente, activo, técnico, tipo, prioridad y estado |
| `activos-intervenidos` | Activos con su cliente y la cantidad de órdenes |
| `repuestos-utilizados` | Repuestos asociados a las órdenes y en cuántas órdenes se usó cada uno |
| `actividad-tecnicos` | Por técnico: órdenes asignadas, finalizadas o cerradas, y pendientes |
| `indicadores` | Resumen (total y tiempos promedio), órdenes por estado, prioridad y tipo, actividad por técnico y activos intervenidos |

Acepta los mismos filtros que `/indicadores` y además `prioridad` (`baja`, `media` o `alta`), que también vale para `/indicadores`. Errores: `400` con `TIPO_REPORTE_INVALIDO` si el tipo no existe y `RANGO_FECHAS_INVALIDO` si las fechas están invertidas.

Al agregar un paquete, hay que reconstruir la imagen del backend (`docker compose up -d --build backend`) para restaurarlo.

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

## Cambios de esquema en bases existentes

Con Docker, `db-datos` recrea el esquema `operativo` cada vez que cambia `01-init.sql`, así que no hace falta aplicar nada a mano. Para una base que no se carga así (por ejemplo la del VPS), los cambios están en `database/cambios/`, con fecha en el nombre, y se pueden ejecutar más de una vez sin error:

```bash
docker exec -i sotra-db psql -U TU_USUARIO -d sotra < database/cambios/2026-10-17-usuario-id-keycloak.sql
docker exec -i sotra-db psql -U TU_USUARIO -d sotra < database/cambios/2026-10-17-usuario-sin-contrasena.sql
```

| Cambio | Qué hace |
|---|---|
| `2026-10-17-usuario-id-keycloak.sql` | Columna `id_keycloak` (uuid, única) en `operativo.usuario`. |
| `2026-10-17-usuario-sin-contrasena.sql` | Elimina la columna `contrasena` de `operativo.usuario`: la contraseña vive solo en Keycloak. Hay que aplicarla con la versión de la API que ya no usa esa columna (detener el backend, migrar y volver a levantarlo). |

## Detener el sistema

Detener temporalmente los contenedores:

```bash
docker compose stop
```

Iniciarlos nuevamente:

```bash
docker compose start
```

Detener y eliminar los contenedores sin borrar los datos:

```bash
docker compose down
```

Los datos de la base, incluida la configuración de Keycloak, quedan en el volumen `postgres_data`. `docker compose down -v` también borra el volumen y deja la base vacía: la próxima vez se vuelve a crear con los scripts de `database/init/`.

## Diagnóstico de las sugerencias de IA

Si `POST /api/v1/ordenes-trabajo/sugerencia` responde `502`, mirar el campo `codigo`:

- `SERVICIO_IA_NO_DISPONIBLE`: Ollama no responde. Comprobar que corre y que `OLLAMA_BASE_URL` es alcanzable **desde el contenedor** del backend: en `compose.yml` apunta a `host.docker.internal`, así que Ollama debe escuchar en el equipo anfitrión (por defecto el puerto 11434, con los modelos `bge-m3` y `qwen2.5:7b`). `curl http://localhost:11434/api/tags` debe responder. El detalle queda en el log como advertencia.
- `SUGERENCIA_FALLIDA`: otro error en la búsqueda, el reranker o la generación; el log del backend trae la excepción (`LogError`) con el identificador de correlación.
- `422 SIN_CONTEXTO_TECNICO`: el servicio funciona, pero ningún documento técnico procesado cubre la tarea.

Las pruebas de integración apuntan `OLLAMA_BASE_URL` a un puerto cerrado para comprobar el `502` sin depender de Ollama.

## Pruebas unitarias (`Api.PruebasUnitarias`)

Proyecto NUnit aparte de `Api.Pruebas` (las de integración levantan Testcontainers para todo su espacio de nombres). No usa base de datos, Docker ni red, y tarda alrededor de un segundo:

```bash
dotnet test Api.PruebasUnitarias
```

En la ejecución local 1.0.38 pasan las 363 pruebas de la selección sin `CorpusDeEvaluacionIaPruebas` (sus dos pruebas buscan la carpeta antigua `herramientas-plan`). Incluye 27 regresiones nuevas de contratos PUT/PATCH, además de validaciones, mapeadores, stock, órdenes, reportes, manuales, infraestructura, resiliencia y observabilidad. Resultados y limitaciones en `../sotra-test-automation/Estado de las pruebas locales.md`.

Clientes tiene 12 pruebas nuevas de validación de sus DTO (obligatorios, límites de longitud, opcionales, correo y teléfono de 8 dígitos, también al modificar), además de las 5 pruebas de mapeo existentes. Las 17 pasan. Ejecutar solo esta selección:

```bash
dotnet test Api.PruebasUnitarias --filter "FullyQualifiedName~Api.PruebasUnitarias.ClientesPruebas|FullyQualifiedName~MapeadoresPruebas.Cliente_"
```

## Pruebas de seguridad, de casos y de carga locales

Scripts en `../herramientas-plan/` que usan una base descartable (`sotra-casos-pg`, puerto 55432; nunca la base local con datos): `iniciar_base_casos.sh` la crea, `correr_casos_api.sh` levanta una copia de la API y ejecuta los casos de API exportados de Bugasura (solo lectura, no escribe en Bugasura), `fuzz_seguridad_api.py` prueba inyecciones, XSS, tipos, campos extra, cuerpos malformados, subidas hostiles, cabeceras y CORS en todos los endpoints, y `carga_api.py` simula 20 usuarios con `datos_volumen.sql` (30 000 órdenes). Resultados en `../RESULTADOS-PRUEBAS-LOCALES.md`.

Rendimiento: `OrdenTrabajoDAO.Filtrar` es la base compartida del listado y de los reportes; `ListarResumenes` proyecta solo los campos necesarios y `ListarRepuestosUsados` agrupa en la base. Los índices están en `database/cambios/2026-10-16-indices-rendimiento.sql` (aplicar a mano).

## Concurrencia en las órdenes de trabajo

`PATCH /ordenes-trabajo/{id}/estado` y `PATCH /ordenes-trabajo/{id}/tecnico` (`OrdenTrabajoDAO.CambiarEstado` y `AsignarTecnico`) abren una transacción, bloquean la fila de la orden con `SELECT ... FOR UPDATE` y vuelven a leerla antes de validar la transición. Así dos cambios simultáneos se ejecutan en serie. La prueba `CincoCambiosDeEstadoSimultaneosAlMismoDestinoSoloSeAplicanUnaVez` falla sin el bloqueo (cinco aplicaciones en lugar de una). `PUT /ordenes-trabajo/{id}` (`OrdenTrabajoDAO.Actualizar`, que ahora recibe los cambios como `CambiosDeOrden` y devuelve `null` si la orden no existe) usa el mismo bloqueo: el controlador ya no modifica la entidad antes de entrar a la transacción.

## Dashboard analítico

`AnaliticaController` expone `GET /api/v1/reportes/tendencias`, `/tiempos` (con duración, tiempo por etapa y cumplimiento de fechas previstas), `/reincidencias`, `/repuestos-usados` y `/alertas-maquinaria` (contratos en `Documentacion/Flujo de datos SOTRA.md`, sección 16). Todo se calcula en la base con `GROUP BY`, `percentile_cont` y funciones de ventana (`LAG` y `LEAD`) en `AnaliticaDAO`, con consultas SQL constantes y parámetros tipados (`@desde`, `@clienteId`, etc.): no se arma SQL con texto de la persona. Los umbrales de las alertas y los días de reincidencia se configuran con la sección `AlertasMaquinaria` (variables `AlertasMaquinaria__SinAsignarHoras`, `__AsignadaSinIniciarHoras`, `__PausadaHoras`, `__MaquinaDetenidaAltaHoras`, `__MaquinaDetenidaMediaHoras`, `__MaquinaDetenidaBajaHoras` y `__ReincidenciaDias`), validada al iniciar (`OpcionesDeAlertasDeMaquinaria`). Los valores por defecto son provisionales hasta acordarlos con el área de postventa.

**Caché corta.** Las cinco consultas (`tendencias`, `tiempos`, `repuestos-usados`, `reincidencias` y `alertas-maquinaria`) reutilizan el resultado 30 segundos para los mismos filtros (`CacheAnalitica__Segundos`, de 0 a 600; con 0 queda apagada), así que un dato nuevo puede tardar hasta ese tiempo en verse. Las solicitudes simultáneas con los mismos filtros comparten un único cálculo, un error no se guarda y cancelar una solicitud no interrumpe el cálculo de las demás. Código en `Api/Infraestructura/CacheDeAnalitica.cs`; pruebas en `Api.PruebasUnitarias/CacheDeAnaliticaPruebas.cs`.

## Auditoría de acciones

`AuditoriaDeAcciones` (`Api/Infraestructura/AuditoriaDeAcciones.cs`) es un filtro global que registra, con `ILogger` (Serilog), cada acción que cambia datos (`POST`, `PUT`, `PATCH` y `DELETE`): la acción, el controlador, el id del recurso de la ruta, el código HTTP del resultado y el correo de quien la pidió (`sin autenticar` mientras la seguridad esté apagada). Se registran también las acciones rechazadas (`4xx`) y las que fallan (`500`). No guarda el cuerpo de la solicitud ni datos personales. Cada línea lleva el `IdCorrelacion`, así que se puede seguir una acción de punta a punta. Ejemplo: `Auditoria PATCH CambiarEstado OrdenesTrabajo recurso 45 resultado 200 usuario sin autenticar`. Cubierto por `AuditoriaPruebas` y `SeguridadPruebas`.
