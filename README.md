# GamingHub (WinUI 3 · .NET 8 · MVVM · JSON)

App de escritorio gaming inspirada en Steam. **No usa bases de datos**: todo se guarda en JSON en `%LocalAppData%\GamingHub\data`.

## Requisitos
- Windows 10 (1809+) u 11, x64.
- Visual Studio 2022 17.8 o superior (Community sirve) con la carga de trabajo **Desarrollo de escritorio de .NET** y el componente **Windows App SDK C# Templates** (incluye el SDK de Windows 10.0.19041 o superior).
- .NET SDK 8.0.
- Acceso a NuGet la primera vez (restaura WindowsAppSDK 1.6, CommunityToolkit.Mvvm 8.3.2, Microsoft.Extensions.*, xUnit, FluentAssertions 6.12.1, NSubstitute).

## Ejecutar
1. Abre `GamingHub.sln`.
2. Plataforma de la solución: **x64** (solo está mapeada x64). Proyecto de inicio: `GamingHub.App`.
3. Restaura paquetes y pulsa F5. La app es *unpackaged* y autocontenida (no requiere instalar MSIX).
4. Pruebas: menú Prueba > Explorador de pruebas, o `dotnet test tests/GamingHub.Tests`.

## Estructura
- `src/GamingHub.Core`: modelos, validadores centralizados, seguridad (PBKDF2-SHA256, tokens, sesiones), almacenamiento JSON (`JsonFileStore`), servicios con control de permisos.
- `src/GamingHub.App`: WinUI 3 (Views, ViewModels MVVM con CommunityToolkit, DI, log a archivo).
- `tests/GamingHub.Tests`: xUnit + FluentAssertions + NSubstitute.
- `docs/`: matriz de pruebas e informe.

## Reglas clave
- El **primer usuario registrado es administrador local**; el resto son usuarios normales.
- Contraseña 12–64, hash PBKDF2-SHA256 (310 000 iteraciones, sal por contraseña). Bloqueo temporal progresivo tras 5 fallos (30 s, 60 s… máx. 15 min).
- Recuperación de contraseña **local**: el token se muestra en pantalla porque no existe servidor de correo.
- Compras y chat son **locales/simulados** y así se indican en la interfaz. El catálogo son títulos «Demo».
- Datos: `users.json, games.json, library.json, friends.json, messages.json, notifications.json, purchases.json, sessions.json, settings.json, activity.json` (se crean solos; con `.bak` y escritura atómica).

## Tickets de compra
- Al pagar (tarjeta o transferencia) se genera un **ticket** con número, fecha, cliente, juegos (categoría, desarrollador, plataforma, requisitos, tamaño, lanzamiento), total y datos del pago.
- **Tarjeta**: se guarda solo marca y 4 últimos dígitos. **Transferencia**: ordenante, banco, 4 últimos dígitos de la CLABE, asunto y clave de rastreo. El número completo de tarjeta y el CVV **nunca** se guardan.
- El diálogo del ticket permite **Imprimir** (abre el cuadro de impresión del navegador; ahí también se puede guardar como PDF) y **Abrir carpeta**. Copias en `%LocalAppData%\GamingHub\data\tickets` (`.txt` y `.html`).
- En *Tienda > Historial de compras* cada compra nueva tiene **Ver ticket** para reimprimirlo.
- Código: `Core/Services/Tickets.cs` (generador, render texto/HTML, archivos), `Core/Validation/Validators.cs` (`PaymentValidator`), `App/Views/CheckoutDialog` y `TicketDialog`. Pruebas en `tests/GamingHub.Tests/PaymentAndTicketTests.cs`.

