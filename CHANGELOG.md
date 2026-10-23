# CHANGELOG

## Version 1.0.55 (2026-10-23)

- Pruebas: se dejan las comprobaciones de reactivación de usuarios para su rama correspondiente, junto con esa funcionalidad.

## Version 1.0.54 (2026-10-23)

- Usuarios: se acepta el celular con cero inicial y se guarda normalizado; el alta informa si Keycloak envió el correo y el reenvío avisa cuando falla.

## Version 1.0.53 (2026-10-23)

- Integración: se conserva el formato de validación de la API tras registrar los controladores y las variables del entorno tienen prioridad sobre el archivo local de configuración.
- Pruebas: se corrige la ubicación del corpus de evaluación y se hace explícita la selección del mapeador en el ejecutor de regresión después de integrar Keycloak.
