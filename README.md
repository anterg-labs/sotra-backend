## Configurar PostgreSQL con archivo `.env`

1. Copiar el archivo `.env.example` en la raiz del proyecto y renombrarlo como `.env`.

2. Editar `.env` con los datos del entorno:

```env
DB_HOST=localhost
DB_PORT=5432
DB_NAME=sotra
DB_USER=TU_USUARIO
DB_PASSWORD=TU_PASSWORD
````

> Si el backend también se ejecuta dentro de Docker Compose, `DB_HOST` debe ser `db`.

4. Verificar que `SotraContext.cs` lea las variables desde `.env`.
   El proyecto está preparado para cargar estas variables mediante DotNetEnv.

5. No subir `.env` al repositorio.
   El archivo `.gitignore` debe excluirlo.

6. Comprobar que el proyecto compile:

```bash
dotnet build
```

7. Comprobar Entity Framework:

```bash
dotnet ef dbcontext info --context SotraContext
```

### Importante

* No escribir usuario ni contraseña directamente en `SotraContext.cs`.
* Cada desarrollador debe tener su propio archivo `.env` local.
* `.env.example` sí debe versionarse como plantilla.
* Si cambian los datos de conexión, solo debe actualizarse el `.env` local.

---

## Levantar el entorno con Docker Compose

Para construir las imágenes y levantar todos los servicios:

```bash
docker compose up --build
```

Para ejecutarlos en segundo plano:

```bash
docker compose up -d --build
```

Para levantar solamente PostgreSQL:

```bash
docker compose up -d db
```

---

## Detener el entorno

### Detener temporalmente

```bash
docker compose stop
```

Detiene los contenedores, pero no los elimina. Pueden iniciarse nuevamente con:

```bash
docker compose start
```

### Detener y eliminar los contenedores

```bash
docker compose down
```

Detiene y elimina los contenedores y la red creada por Docker Compose, pero conserva los volúmenes y los datos de PostgreSQL.

### Detener y eliminar también los datos

```bash
docker compose down -v
```

Además de eliminar los contenedores y la red, elimina los volúmenes.

> `docker compose down -v` elimina los datos persistidos de PostgreSQL. Utilizarlo con cuidado.