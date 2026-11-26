# Manual Técnico — Fiadex

**Versión 1.0 — 19/11/2026**

## 1. Objetivo y alcance

Este documento describe la arquitectura, el modelo de datos, las reglas de negocio, los servicios y la estrategia de pruebas de Fiadex. Está dirigido a desarrolladores y personal de soporte que deban mantener o modificar la aplicación.

Para instrucciones de uso, consulta `MANUAL_USUARIO.md`. Para generar y distribuir el paquete, consulta `MANUAL_DESPLIEGUE.md`.

---

## 2. Stack tecnológico

| Capa | Tecnología |
|---|---|
| Interfaz | WPF (Windows Presentation Foundation), XAML |
| Lenguaje | C# con `net10.0-windows` |
| Patrón | MVVM con `CommunityToolkit.Mvvm` 8.4.2 |
| Persistencia | SQLite mediante `Microsoft.Data.Sqlite` 11.0.0 (`SQLitePCLRaw` + `e_sqlite3`) |
| Pruebas | xUnit (`Fiadex.Tests`) |
| IA (opcional) | Ollama local por HTTP: `http://localhost:11434`, modelo `qwen2.5:3b` |
| Distribución | Publicación autocontenida `win-x64`, empaquetada en ZIP por `publish.ps1` |

El proyecto es **mono usuario**, funciona **sin conexión** y no tiene servidor ni sincronización entre equipos.

---

## 3. Estructura del repositorio

```text
Fiadex/
├── Fiadex.slnx                    Solución
├── publish.ps1                    Genera el ZIP portable
├── README.md                      Resumen general
├── MANUAL_USUARIO.md
├── MANUAL_TECNICO.md
├── MANUAL_DESPLIEGUE.md
├── Fiadex/                        Proyecto principal
│   ├── App.xaml                   Recursos globales, estilos y convertidores
│   ├── App.xaml.cs                Arranque: muestra LoginWindow
│   ├── MainWindow.xaml/.cs        Shell, navegación y siete módulos
│   ├── LoginWindow.xaml/.cs       Alta de administrador y acceso
│   ├── ChangePasswordWindow.*     Cambio de contraseña
│   ├── PasswordField.xaml/.cs     PasswordBox + TextBox + ojo mostrar/ocultar
│   ├── EditPersonWindow.*         Edición de persona
│   ├── EditLoanWindow.*           Edición de préstamo
│   ├── EditPaymentWindow.*        Edición de abono
│   ├── MainViewModel.cs           Estado, comandos, filtros y validación inline
│   ├── Models.cs                  Person, Loan, Payment, LoanSummary, DTOs
│   ├── LoanCalculator.cs          Cálculo de saldos
│   ├── IFiadexDatabase.cs         Contrato de la capa de datos
│   ├── FiadexDatabase.cs          Esquema, CRUD y reglas de negocio
│   ├── FiadexDatabase.Auth.cs     Usuarios, hash, bloqueo y desbloqueo
│   ├── FiadexDatabase.Analytics.cs Dashboard, mediana y analítica mensual
│   ├── FiadexDatabase.Aggregates.cs Refresco de tablas agregadas
│   ├── PasswordSecurity.cs        PBKDF2 y política de contraseña
│   ├── SessionService.cs          Duración e inactividad de sesión
│   ├── ChatService.cs             Cliente Ollama, herramientas y validación
│   ├── ToastService.cs            Notificaciones en pantalla
│   └── Converters.cs              Convertidores XAML
└── Fiadex.Tests/                  Pruebas xUnit
    ├── UnitTest1.cs
    └── AdditionalTests.cs
```

---

## 4. Arquitectura y flujo de arranque

### 4.1 Capas

1. **Vista (XAML + code-behind)**: ventanas, estilos, convertidores y eventos de edición/eliminación.
2. **ViewModel**: `MainViewModel` expone propiedades observables y comandos `RelayCommand`. Valida antes de llamar a la base y publica mensajes de éxito/error.
3. **Datos**: `IFiadexDatabase` / `FiadexDatabase` (clase `partial` repartida en cuatro archivos). Toda la lógica SQL y las reglas de integridad están aquí, no solo en la interfaz.
4. **Servicios**: sesión, contraseñas, notificaciones y asistente IA.

### 4.2 Arranque

```text
App.OnStartup()
  └─> new LoginWindow().Show()
        ├─> FiadexDatabase()  → ruta por defecto %LOCALAPPDATA%\Fiadex\fiadex.db
        ├─> Initialize()      → CREATE TABLE IF NOT EXISTS + consolidación + agregados
        └─> HasUser()
              ├─> false: modo "Configure la cuenta administradora"
              └─> true : autenticación
                    └─> MainWindow(username)
                          ├─> DataContext = new MainViewModel(database)
                          ├─> SessionService.Start()
                          └─> DispatcherTimer de 30 s que vigila la sesión
```

La ruta de la base se calcula en `GetDefaultDatabasePath()`:

```csharp
Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "Fiadex",
    "fiadex.db")
```

Para pruebas se usa el constructor `new FiadexDatabase(rutaTemporal)`, que crea el directorio si no existe.

### 4.3 Navegación

`ListBoxItem`s de la barra lateral con `Tag` = `Dashboard`, `Personas`, `Préstamos`, `Historial`, `Abonos`, `Comprobantes`, `Asistente IA`. El `SelectionChanged` sincroniza el `TabControl` principal y el selector del encabezado. El orden debe coincidir en tres sitios: los `ListBoxItem`, la colección `Sections` de `MainViewModel` y los `TabItem` de `MainTabs`.

La barra colapsada usa `SidebarWidthConverter` (64 px vs 230 px) y convertidores de visibilidad para mostrar solo iconos centrados.

---

## 5. Modelo de datos

### 5.1 Tablas operativas

| Tabla | Columnas | Restricciones |
|---|---|---|
| `Persons` | `Id`, `Name`, `Phone`, `CreatedAt` | `Id` autoincremental, `Name NOT NULL`, `Phone NOT NULL DEFAULT ''` |
| `Loans` | `Id`, `PersonId`, `AmountCents`, `LoanDate`, `Notes` | `AmountCents >= 0`, FK a `Persons(Id)` |
| `Payments` | `Id`, `LoanId`, `AmountCents`, `PaymentDate`, `Notes` | `AmountCents >= 0`, FK a `Loans(Id)` |
| `Users` | `Id`, `Username`, `PasswordHash`, `PasswordSalt`, `FailedAttempts`, `IsLocked`, `LastLoginAt` | `Username UNIQUE` |

Índices: `IX_Loans_PersonId` e `IX_Payments_LoanId`.

Los importes se guardan como **enteros en centavos** (`ToCents` / `FromCents`, redondeo `MidpointRounding.AwayFromZero`) para evitar errores de coma flotante. Las fechas se serializan en formato ISO `"O"`.

### 5.2 Tablas agregadas

| Tabla | Contenido |
|---|---|
| `LoanAggregates` | `LoanId`, `PersonId`, `PaidCents`, `BalanceCents = MAX(0, AmountCents - PaidCents)` |
| `PersonAggregates` | `PersonId`, `LoanTotalCents`, `PaidTotalCents`, `LoanCount`, `PaymentCount` |
| `MonthlyAggregates` | `Year`, `Month`, `LoanCents`, `PaymentCents` (PK compuesta) |

`RefreshAggregates()` se ejecuta al inicializar y después de cada mutación (altas, ediciones y borrados). Reconstruye las tres tablas con `DELETE` + `INSERT ... SELECT`.

### 5.3 Una sola cuenta por persona

`Initialize()` llama a `ConsolidateLoanAccounts(connection)`:

1. Detecta personas con `COUNT(*) > 1` en `Loans`.
2. Elige como cuenta destino el préstamo de menor `Id`.
3. Suma los `AmountCents`, conserva la `LoanDate` más antigua y concatena notas únicas con `" | "`.
4. Mueve los `Payments` de los préstamos duplicados a la cuenta destino.
5. Elimina las filas duplicadas.

`AddLoan` mantiene esta regla: si ya existe préstamo para la persona, suma el monto, conserva la fecha más antigua y agrega la nota `Préstamo adicional de {monto} registrado el {fecha}`.

> Consecuencia operativa: al abrir una base antigua con duplicados, estos se fusionan automáticamente. Por eso se recomienda respaldar `fiadex.db` antes de migrar.

---

## 6. Reglas de negocio y validación

La validación existe en **tres niveles**: validación inline del ViewModel, restricciones en los `DatePicker` de las ventanas y comprobaciones definitivas en `FiadexDatabase`.

### 6.1 Préstamos

| Regla | Mensaje |
|---|---|
| Monto > 0 | `El monto debe ser mayor que cero.` |
| Fecha no futura | `La fecha del préstamo no puede ser futura.` |
| Al editar, monto >= abonos | `El monto no puede ser menor que los abonos registrados ({Paid:C0}).` |
| Al editar, fecha <= abono más antiguo | `La fecha del préstamo no puede ser posterior al abono más antiguo ({fecha:dd/MM/yyyy}).` |

### 6.2 Abonos

| Regla | Mensaje |
|---|---|
| Monto > 0 | `El abono debe ser mayor que cero.` |
| No excede el saldo | `El abono excede el saldo pendiente de {balance:C0}.` |
| Fecha no futura | `La fecha del abono no puede ser futura.` |
| Fecha >= préstamo | `La fecha del abono no puede ser anterior a la fecha del préstamo ({fecha:dd/MM/yyyy}).` |
| Al editar, no excede el saldo disponible | `El abono excede el saldo disponible de {…:C0}.` |

`ValidatePaymentDate(paymentDate, loanDate)` se usa tanto en altas como en ediciones. `UpdatePayment` descuenta el abono que se está editando antes de comparar, para no contarlo dos veces.

### 6.3 Cálculo de saldo

`LoanCalculator.CalculateBalance(principal, payments)` devuelve `Max(0, principal - payments)` y rechaza principal negativo. `LoanSummary.Balance` es de solo lectura.

### 6.4 Estados de deuda

- `BalanceStatusConverter` → `"Pagado"` si `balance <= 0`, otherwise `"Pendiente"` (columnas `Estado`).
- `MainViewModel.ReportLoanStatus` → `"{acción} El préstamo ya está pagado."` o `"{acción} Aún debe {balance:C0}."` (toasts y barra de estado).

---

## 7. Seguridad

### 7.1 Contraseñas

`PasswordSecurity`:

- PBKDF2 con SHA-256, **100 000 iteraciones**, salt aleatorio de 16 bytes y hash de 32 bytes.
- Comparación en tiempo constante (`CryptographicOperations.FixedTimeEquals`).
- Política: 12 caracteres mínimo, mayúscula, minúscula, número y símbolo.
- Ninguna credencial se guarda en texto plano ni en el repositorio.

`PasswordField` es un `UserControl` con un `PasswordBox` enmascarado y un `TextBox` superpuesto. Ambos se sincronizan mediante un guardia `_syncing` para evitar bucles. El botón del ojo alterna visibilidad, actualiza `ToolTip`, `AutomationProperties.Name` y devuelve el foco al control visible. La propiedad pública `Password` permite leer/escribir el valor desde las ventanas.

### 7.2 Bloqueo de cuenta

`Authenticate`:

- Si `IsLocked = 1` → excepción `La cuenta está bloqueada. Solicite recuperación al administrador.`
- Si falla, incrementa `FailedAttempts`; al llegar a 5 marca `IsLocked = 1`.
- Si acierta, reinicia intentos, desbloquea y actualiza `LastLoginAt` en UTC.
- `UnlockAccount(username)` reinicia intentos y bloqueo sin verificar contraseña (recuperación local del administrador).

### 7.3 Sesión

`SessionService`:

- `MaximumDuration = 8 horas` (renovación deslizante en cada actividad).
- `InactivityLimit = 15 minutos`.
- `MainWindow` registra actividad en `PreviewMouseMove` y `PreviewKeyDown`; un `DispatcherTimer` de 30 s llama a `IsActive()` y cierra la sesión si expiró.
- No hay 2FA ni API keys.

---

## 8. Dashboard y analítica

`GetDashboard(month)` consulta las tablas agregadas y devuelve:

- `PendingTotal`: suma de `BalanceCents`.
- `PaymentsThisMonth`: `MonthlyAggregates` del año/mes actuales.
- `ActiveLoans`: cuentas con `BalanceCents > 0`.
- `PaymentCount` y `LoanCount`: totales de `PersonAggregates`.
- `MissingFields`: `GetMissingFieldsCount()` cuenta personas sin teléfono y movimientos sin notas.

`GetMonthlyAnalytics(12)` genera 12 puntos **en orden cronológico** (de más antiguo a más reciente) con nombres de mes en español (`es-ES`) y altura de barra proporcional al máximo de abonos. En la interfaz, las barras muestran la etiqueta del mes y la gráfica de línea (`DrawMonthlyLine` en `MainWindow.xaml.cs`) dibuja cuadrícula horizontal, escala, etiquetas de mes, un punto por mes y centra el punto cuando solo hay un mes con datos. `GetMedianLoanAmount()` calcula la mediana de los importes ordenados.

Los antiguos contadores de errores y de uso por módulo se retiraron del Dashboard y de `MainViewModel`; `MissingFields` se conserva porque `ChatService` lo usa para contextualizar al asistente IA.

---

## 9. Asistente IA

`ChatService`:

- `HttpClient` con `BaseAddress = http://localhost:11434/` y modelo `qwen2.5:3b` (ambos inyectables por constructor para pruebas).
- `AskAsync` usa el modo sin herramientas y `BuildReadOnlyFacts(question)` inyecta cifras locales en el prompt.
- `SummarizeAsync` usa herramientas de solo lectura (`get_dashboard`, `get_person_history`, `get_loans`) con un máximo de `MaxToolRounds = 2`.
- Las respuestas se validan: los números que no aparecen en los datos locales se sustituyen por un aviso (`AskAsync_RejectsNumbersNotPresentInLocalData`).
- Memoria de conversación de sesión (`_history`); `ClearHistory()` la reinicia.
- Errores controlados: `HttpRequestException` → mensaje de Ollama no disponible; `TaskCanceledException` → “La consulta tardó demasiado tiempo.”

Para usar herramientas se recomienda un modelo con soporte *Tools*, por ejemplo `qwen3`.

---

## 10. Interfaz y accesibilidad

- Paleta monocromática definida en `App.xaml` (`#18181B` texto principal, `#62626A` secundario, `#6C6C74` atenuado, etc.). No se usa `Foreground` global en `TextBlock` porque rompía el texto de los botones.
- Estilos globales para `TextBox`, `PasswordBox`, `ComboBox`, `Button` y `DataGrid`.
- `DataGrid.IsReadOnly = True` a nivel global (`App.xaml`, línea 419). No existen handlers `MouseDoubleClick`; la edición se hace únicamente desde los botones `Editar…`.
- Ventanas de edición con `ScrollViewer` + pie fijo de 38 px para que **Cancelar / Guardar cambios** nunca se recorten.
- Botones medidos: `Cancelar` 110×38, `Guardar cambios` 150×38, `Guardar nueva contraseña` 190×38.
- Auditoría visual automatizada: **0 problemas en 527 elementos** (contraste, recortes y controles).

---

## 11. Pruebas

```powershell
dotnet test Fiadex.slnx
```

Última ejecución: **30/30 correctas**. Cobertura por clases:

| Clase | Qué verifica |
|---|---|
| `LoanCalculatorTests` | Cálculo de saldo y rechazo de principal negativo. |
| `ChatServiceTests` | Uso exclusivo de datos locales. |
| `PasswordSecurityTests` | Hash/verificación y política de contraseña. |
| `Database_LocksAfterFiveFailedAttempts` | Bloqueo por intentos fallidos. |
| `RecordCrudTests` | Analítica mensual, historial, recálculo y borrados. |
| `SessionServiceTests` | Renovación de 8 h y expiración por 15 min. |
| `AggregateStorageTests` | Sincronía de tablas agregadas tras mutaciones. |
| `ChatToolTests` | Herramientas de solo lectura y validación de cifras. |
| `InlineValidationTests` | Validación de comandos antes de guardar. |
| `DateConsistencyTests` | Fechas en altas y ediciones. |
| `PaymentFeedbackTests` | Mensajes de saldo pendiente y pagado. |
| `SingleAccountTests` | Acumulación y consolidación de cuentas duplicadas. |
| `PersonSearchTests` | Búsqueda por nombre/teléfono y limpieza. |
| `ClientHistoryTests` | Historial filtrado por cliente seleccionado. |

Compilación Debug y Release: 0 advertencias, 0 errores.

---

## 12. Puntos de extensión

| Necesidad | Dónde tocar |
|---|---|
| Cambiar colores o estilos | `Fiadex/App.xaml` |
| Agregar una regla de negocio | `FiadexDatabase.cs` (y reflejarla en `MainViewModel` y en la ventana de edición) |
| Nuevo campo persistente | `Initialize()` + `Models.cs` + `FiadexDatabase.cs` + XAML; luego `RefreshAggregates()` si afecta a métricas |
| Cambiar modelo/URL de Ollama | Constructor de `ChatService` (`model`, `baseUrl`) |
| Cambiar ruta de la base | `GetDefaultDatabasePath()` o constructor con ruta |
| Agregar un módulo de navegación | `ListBoxItem` en la barra lateral + cadena en `MainViewModel.Sections` + `TabItem` en el mismo orden |
| Modificar el ojo de contraseña | `PasswordField.xaml` / `.xaml.cs` |

---

## 13. Glosario

- **Cuenta**: préstamo único de una persona; puede acumular varios desembolsos.
- **Abono**: pago parcial o total aplicado a una cuenta.
- **Saldo**: `MAX(0, monto - abonado)`.
- **Agregado**: tabla derivada que acelera dashboard e historial.
- **Consolidación**: fusión automática de préstamos duplicados de una misma persona.
