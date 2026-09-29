# Verificaciones de regresión

Ejecutar desde la raíz de la solución:

```powershell
dotnet run --project PruebasRegresion/PruebasRegresion.csproj
```

El ejecutable termina con error si falla una comprobación. Usa SQLite en memoria; no lee ni modifica la base PostgreSQL configurada. Verifica altas con un único SaveChanges, roles, contraseñas con hash, edición con entidades rastreadas y desconectadas, fechas, listado completo, contrato JSON, baja lógica y eliminación. Las secuencias de PostgreSQL se sustituyen únicamente en el modelo de pruebas.

El proveedor SQLite no sustituye las comprobaciones de integración con PostgreSQL. Las solicitudes GET de Api/Api.http permiten comprobar el listado en la API configurada.

## Contrato del listado

GET /api/v1/usuarios devuelve todos los usuarios ordenados por identificador, sin paginación. El arreglo conserva los campos id, nombre, apellido, correo, telefono, estado, fechaIngreso y roles. Los roles conservan id, nombre y activo. fechaIngreso usa yyyy-MM-dd.

## Contraseñas

InsertarUsuario recibe la contraseña en texto y guarda un hash mediante PasswordHasher de ASP.NET Core Identity. ActualizarUsuario recibe el cambio mediante el parámetro opcional nuevaContrasena; null conserva la contraseña almacenada. No se debe asignar una contraseña nueva directamente a una entidad rastreada. La consola utiliza este parámetro y no muestra la contraseña al escribir ni el valor existente al editar.

Contrasenas.Verificar permite comprobar los nuevos hashes. Los valores antiguos de la base no se migran automáticamente y no son aceptados como hashes: necesitan un restablecimiento de contraseña o una migración controlada al incorporar autenticación. Esta solución todavía no implementa un endpoint de login.
