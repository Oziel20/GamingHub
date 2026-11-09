# Informe final (honesto)

## Estado de verificación
- **Compilación: NO verificada.** El proyecto se escribió en un entorno Linux sin .NET SDK, sin Windows y sin NuGet. Nadie ha compilado esta solución todavía.
- **Pruebas: NO ejecutadas.** Hay 62 pruebas automatizadas escritas (ver `MATRIZ_DE_PRUEBAS.md`), todas BLOCKED. No hay pruebas aprobadas ni fallidas confirmadas.
- **Ejecución de la interfaz: NO verificada.** El XAML y los bindings (`x:Bind`) pueden tener errores que solo aparecen al compilar.
- Se revisó el código a mano, pero es esperable corregir errores de compilación (nombres, versiones de paquetes, XAML) en la primera apertura.

## Implementado
Login, registro, recuperación local, sesiones con expiración/revocación, bloqueo temporal progresivo, dashboard, notificaciones, historial de actividad, biblioteca (alta de juegos locales .exe, búsqueda 2–100, filtro, orden, paginación, favoritos, instalación simulada, jugar con seguimiento de tiempo), tienda (catálogo, búsqueda, carrito, compra simulada, historial), amigos (solicitudes, aceptar, rechazar, eliminar, bloquear), chat local (2000 caracteres, límite de frecuencia), perfil (nombre, descripción, avatar validado por extensión y firma), configuración y administración local (directorios autorizados, desbloqueo, revocación de sesiones).

## Medidas de seguridad
PBKDF2 con sal; tokens aleatorios guardados solo como SHA-256; mensajes de error genéricos en login; comprobación de permisos en cada servicio (no en la UI); validación de rutas (directorios autorizados, extensión, firma MZ, enlaces simbólicos); validación de imágenes; log sin contraseñas ni stack traces.

## Limitaciones y riesgos conocidos
1. Sin verificar compilación ni pruebas (ver arriba).
2. **No se usa DPAPI** ni cifrado de los JSON: los datos no sensibles y los hashes quedan en texto JSON en AppData. Pendiente.
3. **Sistema de errores parcial**: hay códigos y mensajes (`OperationResult`), pero no existe un catálogo con severidad, descripción interna y contexto por error.
4. Argon2id no está implementado (solo PBKDF2, con iteraciones configurables en el constructor).
5. La recuperación local revela si un correo está registrado y muestra el token en pantalla (inevitable sin correo).
6. Compra: biblioteca y compras son dos archivos; un fallo entre ambas escrituras podría dejar un registro sin historial (se registra en log).
7. El carrito y el límite de mensajes viven en memoria.
8. Sin soporte multiproceso: dos instancias de la app sobre los mismos JSON no están coordinadas.
9. Carátulas: se muestra una inicial en un recuadro; no hay arte real.
10. Pruebas de interfaz, accesibilidad y rendimiento de la UI son manuales y no se han hecho.
11. Los textos de «términos» no existen como documento; la casilla solo registra la aceptación.
12. El `.sln` se escribió a mano: si Visual Studio lo rechaza, crea una solución nueva y añade los 3 proyectos.
