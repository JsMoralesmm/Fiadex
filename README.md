# Fiadex

Aplicación de escritorio para Windows que registra préstamos personales y abonos, calcula saldos pendientes y consulta el historial de pagos. Funciona sin conexión y guarda los datos en SQLite dentro de `%LocalAppData%\Fiadex\fiadex.db`.

## Documentación

Manuales disponibles en **Markdown** y **PDF**:

| Manual | Markdown | PDF |
|---|---|---|
| Usuario | [MANUAL_USUARIO.md](MANUAL_USUARIO.md) | [MANUAL_USUARIO.pdf](Manuales_PDF/MANUAL_USUARIO.pdf) |
| Técnico | [MANUAL_TECNICO.md](MANUAL_TECNICO.md) | [MANUAL_TECNICO.pdf](Manuales_PDF/MANUAL_TECNICO.pdf) |
| Despliegue | [MANUAL_DESPLIEGUE.md](MANUAL_DESPLIEGUE.md) | [MANUAL_DESPLIEGUE.pdf](Manuales_PDF/MANUAL_DESPLIEGUE.pdf) |

También puedes consultar [SUGERENCIAS_MEJORA.md](SUGERENCIAS_MEJORA.md) y [SKILLS.md](SKILLS.md) (especificación técnica original).

## Descarga portable (Release)

- **[Fiadex-win-x64-portable.zip](releases/Fiadex-win-x64-portable.zip)** — Windows x64, autocontenido, sin instalador ni runtime de .NET. Incluye los tres manuales en PDF dentro de la carpeta `Manuales\`.
- Extrae el ZIP en una carpeta propia y ejecuta `Fiadex.exe`. La base de datos se crea en `%LocalAppData%\Fiadex\fiadex.db`.
- SHA-256 de `Fiadex.dll`: `8E20FB8836C348DC5277313E62660A45131EAF72C3B848C5A79772F17F8D1504`.

## Requisitos

- Windows x64.
- .NET 10 SDK (solo para compilar; la versión publicada es autocontenida).
- Ollama en `http://localhost:11434` con el modelo `qwen2.5:3b` para el asistente IA (`ollama pull qwen2.5:3b`). Para usar las herramientas de solo lectura se recomienda un modelo con capacidad *Tools* (por ejemplo `qwen3`); sin él, el asistente responde usando los datos inyectados en el prompt.

## Desarrollo

```powershell
dotnet test Fiadex.slnx
dotnet run --project Fiadex\Fiadex.csproj
```

Al primer inicio se crea la cuenta administradora desde la propia pantalla de acceso. Las contraseñas se almacenan con PBKDF2 (SHA-256, 100 000 iteraciones y sal aleatoria); no se guardan credenciales en el repositorio (`usuarios.txt` está ignorado por Git).

## Publicación portable

```powershell
.\publish.ps1
```

El script ejecuta `dotnet publish -c Release -r win-x64 --self-contained true` y genera `Fiadex-win-x64-portable.zip`. No requiere instalador ni runtime de .NET. La base de datos se guarda fuera de la carpeta del ejecutable, en `%LocalAppData%\Fiadex`.

## Seguridad y sesión

- Usuario/contraseña, política fuerte (12 caracteres, mayúscula, minúscula, número y símbolo).
- Bloqueo tras 5 intentos fallidos y recuperación por el administrador local.
- Sesión de 8 horas con renovación deslizante y cierre por 15 minutos de inactividad.
- Logout global desde la barra lateral; sin 2FA.
- No se usa API key: el modelo es local. No hay sincronización ni resolución de conflictos porque el producto es mono usuario y funciona sin conexión.

## Asistente IA

El chatbot está siempre disponible en la pestaña *Asistente IA*. Solo consulta: el SALDO, los abonos, los préstamos y el historial se calculan en la aplicación. El modelo puede solicitar herramientas de solo lectura (`get_dashboard`, `get_person_history`, `get_loans`); la app ejecuta esas funciones contra SQLite y devuelve el resultado al modelo. Las cifras de la respuesta se validan contra los datos locales: si el modelo inventa un número, se sustituye por un aviso. El botón *Resumen* genera un resumen usando únicamente los datos disponibles. La memoria de conversación es de sesión.

## Estructura

- `Fiadex/`: aplicación WPF (MVVM con CommunityToolkit.Mvvm).
  - `FiadexDatabase*.cs`: capa de datos SQLite (CRUD, autenticación, analítica y tablas agregadas) detrás de `IFiadexDatabase`.
  - `MainViewModel.cs`: estado, comandos, validación inline, toasts y navegación.
  - `ChatService.cs`: integración local con Ollama, herramientas y validación de respuestas.
  - `SessionService.cs`, `PasswordSecurity.cs`: seguridad de sesión y contraseñas.
- `Fiadex.Tests/`: pruebas xUnit de cálculos, base de datos, autenticación, analítica, IA y sesión.

## Repositorio

La rama `main` se mantiene estable; los cambios se trabajan en ramas `feature/*`. Antes de publicar, ejecutar pruebas y generar el ZIP con `publish.ps1`. La release de GitHub debe incluir el ZIP portable y este manual. No subir datos reales de préstamos, contraseñas ni claves.
