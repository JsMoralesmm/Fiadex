# Manual de Despliegue — Fiadex

**Versión 1.0 — 19/11/2026**

## 1. Objetivo

Este manual describe cómo publicar, distribuir, instalar, actualizar, respaldar y revertir Fiadex. El artefacto de entrega es un **ZIP portable autocontenido** que no requiere instalador ni runtime de .NET en el equipo destino.

---

## 2. Artefactos de entrega

| Artefacto | Ruta |
|---|---|
| ZIP portable | `C:\Users\juans\Desktop\Proyecto\Fiadex\Fiadex-win-x64-portable.zip` |
| Carpeta publicada | `C:\Users\juans\Desktop\Proyecto\Fiadex\publish\win-x64` |
| Manuales en PDF | `C:\Users\juans\Desktop\Proyecto\Fiadex\Manuales_PDF` (se copian a `Manuales\` dentro del ZIP) |
| Script de generación | `C:\Users\juans\Desktop\Proyecto\Fiadex\publish.ps1` |

Características del ZIP actual:

- Tamaño aproximado: **62 MiB** (puede variar ligeramente según los manuales incluidos).
- Incluye carpetas de recursos por idioma (`cs`, `de`, `es`, `fr`, etc.) y la carpeta `Manuales\` con los tres PDF.
- Ejecutable principal: `Fiadex.exe`.
- Ensamblado de la aplicación: `Fiadex.dll`.
- SHA-256 de `Fiadex.dll`: `8E20FB8836C348DC5277313E62660A45131EAF72C3B848C5A79772F17F8D1504`.

> El hash cambiará cada vez que se modifique el código. Vuelve a calcularlo tras cada publicación.

---

## 3. Requisitos del equipo destino

### Obligatorios

- Windows **x64**.
- Permisos de escritura en el perfil del usuario, porque la base se crea en:

```text
C:\Users\<usuario>\AppData\Local\Fiadex\fiadex.db
```

- Espacio libre para el ZIP (~62 MiB) y la carpeta extraída (algo más del doble).

### Opcionales (Asistente IA)

- [Ollama](https://ollama.com/) ejecutándose en `http://localhost:11434`.
- Modelo descargado:

```powershell
ollama pull qwen2.5:3b
```

- Para herramientas de solo lectura se recomienda un modelo con soporte *Tools*, por ejemplo:

```powershell
ollama pull qwen3
```

Sin Ollama, todas las funciones de préstamos, abonos e historial siguen funcionando; el asistente muestra un aviso.

---

## 4. Instalación limpia (usuario final)

1. **Cierra cualquier instancia de Fiadex** que esté abierta.
2. Copia `Fiadex-win-x64-portable.zip` al equipo destino.
3. Extrae el ZIP en una carpeta propia, por ejemplo:

```text
C:\Users\<usuario>\Desktop\Fiadex
```

> No ejecutes `Fiadex.exe` desde dentro del ZIP: Windows no permite escribir la configuración ahí y el programa podría fallar.

4. Ejecuta:

```text
C:\Users\<usuario>\Desktop\Fiadex\Fiadex.exe
```

5. En el primer inicio, crea la cuenta administradora (ver `MANUAL_USUARIO.md`, sección 3.1).

La base de datos se crea automáticamente en `%LOCALAPPDATA%\Fiadex\fiadex.db` junto con todas las tablas y los agregados.

---

## 5. Actualización a una versión nueva

La base de datos **no está dentro de la carpeta del programa**, así que normalmente no hay que copiarla al actualizar.

1. Cierra Fiadex por completo.
2. **Recomendado:** respalda la base antes de abrir la versión nueva:

```powershell
Copy-Item "$env:LOCALAPPDATA\Fiadex\fiadex.db" `
          "$env:LOCALAPPDATA\Fiadex\fiadex-backup.db" -Force
```

3. Extrae el ZIP nuevo en una carpeta nueva (por ejemplo `Fiadex-v2`) o reemplaza los archivos de la carpeta actual.
4. Ejecuta el `Fiadex.exe` nuevo.
5. Verifica el historial de un cliente y el ojo de la contraseña.

### Migración automática

Al abrir una base antigua, `Initialize()` ejecuta `ConsolidateLoanAccounts`: fusiona los préstamos duplicados de una misma persona en la cuenta más antigua y mueve sus abonos. Es por esto que el respaldo del paso 2 es recomendable.

### Empezar desde cero

Si se quiere una base vacía sin conservar datos:

```powershell
Remove-Item "$env:LOCALAPPDATA\Fiadex\fiadex.db*" -Force
```

Después, al iniciar Fiadex se creará una base nueva y volverá a pedir la creación de la cuenta administradora.

> No ejecutes dos versiones distintas al mismo tiempo apuntando a la misma base.

---

## 6. Publicación desde el código fuente

### Requisitos del entorno de compilación

- Windows x64.
- **.NET 10 SDK**.
- PowerShell 5.1 o posterior.
- Paquetes NuGet restaurables (`CommunityToolkit.Mvvm`, `Microsoft.Data.Sqlite`).

### Procedimiento

Desde la raíz del repositorio:

```powershell
dotnet test Fiadex.slnx
dotnet build Fiadex\Fiadex.csproj -c Release --nologo
powershell -NoProfile -ExecutionPolicy Bypass -File .\publish.ps1
```

`publish.ps1` ejecuta internamente:

```powershell
dotnet publish Fiadex\Fiadex.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=false `
  -o publish\win-x64
```

Después elimina el ZIP anterior y comprime todo el contenido de `publish\win-x64` en `Fiadex-win-x64-portable.zip`.

### Criterios de aceptación antes de distribuir

- `dotnet test`: **30/30 correctas**.
- `dotnet build -c Release`: **0 advertencias, 0 errores**.
- Auditoría visual: **0 problemas**.
- El ZIP debe contener `Fiadex.exe` y `Fiadex.dll` en la raíz.

### Verificación del paquete

```powershell
$zip = '.\Fiadex-win-x64-portable.zip'
$pub = '.\publish\win-x64\Fiadex.dll'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    $entry = $archive.Entries | Where-Object FullName -eq 'Fiadex.dll'
    $stream = $entry.Open()
    $zipHash = (Get-FileHash -InputStream $stream -Algorithm SHA256).Hash
    $stream.Dispose()
    $pubHash = (Get-FileHash -Path $pub -Algorithm SHA256).Hash
    "ZIP:   $zipHash"
    "Build: $pubHash"
    "Coinciden: $($zipHash -eq $pubHash)"
} finally {
    $archive.Dispose()
}
```

Resultado esperado: `Coinciden: True`.

---

## 7. Despliegue en varios equipos

Fiadex no tiene servidor ni sincronización. Cada equipo ejecuta su propia copia y guarda su propia base en `%LOCALAPPDATA%`.

- Distribuye el mismo ZIP a cada usuario.
- No compartas `fiadex.db` entre equipos mientras estén abiertos.
- Para **trasladar** la información a otro equipo:
  1. Cierra Fiadex en el equipo de origen.
  2. Copia `C:\Users\<origen>\AppData\Local\Fiadex\fiadex.db`.
  3. Cierra Fiadex en el equipo destino.
  4. Reemplaza `C:\Users\<destino>\AppData\Local\Fiadex\fiadex.db`.
  5. Abre Fiadex y comprueba saldos e historial.

No hay resolución de conflictos: la última copia sobrescribe a la anterior.

---

## 8. Respaldo y restauración

### Respaldo

```powershell
$origen = "$env:LOCALAPPDATA\Fiadex\fiadex.db"
$destino = "$env:LOCALAPPDATA\Fiadex\fiadex-$(Get-Date -Format yyyyMMdd-HHmm).db"
Copy-Item $origen $destino -Force
```

Si existen `fiadex.db-wal` o `fiadex.db-shm`, cópialos también (aparecen mientras la base está en uso).

### Restauración

1. Cierra Fiadex.
2. Reemplaza `fiadex.db` por la copia:

```powershell
Copy-Item ".\fiadex-20261119-1200.db" "$env:LOCALAPPDATA\Fiadex\fiadex.db" -Force
```

3. Abre Fiadex y verifica los datos.

### Verificación de integridad

No hay comando propio en la aplicación; puedes usar SQLite desde el entorno de desarrollo:

```powershell
sqlite3 "$env:LOCALAPPDATA\Fiadex\fiadex.db" "PRAGMA integrity_check;"
```

---

## 9. Seguridad en el despliegue

- La contraseña se guarda como PBKDF2-SHA-256 (100 000 iteraciones) con sal aleatoria.
- El archivo `fiadex.db` contiene los datos del negocio: restringe el acceso al perfil del usuario y no lo subas a repositorios ni a unidades compartidas.
- No se guardan credenciales en texto plano ni en archivos del repositorio.
- Si el equipo es compartido, usa **Cerrar sesión**; la sesión caduca sola a los 8 horas o tras 15 minutos de inactividad.
- El puerto de Ollama es local (`localhost:11434`); no hace falta exponerlo en la red.

---

## 10. Checklist posterior al despliegue

- [ ] `Fiadex.exe` abre desde la carpeta extraída.
- [ ] Se crea `%LOCALAPPDATA%\Fiadex\fiadex.db`.
- [ ] Se crea la cuenta administradora y se puede iniciar sesión.
- [ ] El campo de contraseña muestra/oculta el texto con el ojo.
- [ ] Se registra una persona y aparece en **Personas**.
- [ ] Se registra un préstamo y el estado indica *“Aún debe …”*.
- [ ] Se registra un abono y el saldo se actualiza; al pagar todo dice *“ya está pagado”*.
- [ ] El **Historial** muestra solo los préstamos y abonos del cliente elegido.
- [ ] El doble clic en las tablas no edita nada; los botones **Editar…** sí.
- [ ] Si Ollama está instalado, el Asistente IA responde; si no, muestra el aviso correspondiente.
- [ ] Hash de `Fiadex.dll` coincide entre ZIP y `publish\win-x64`.

---

## 11. Reversión (rollback)

1. Cierra Fiadex.
2. Restaura el ZIP de la versión anterior y extráelo en otra carpeta.
3. Restaura la base de datos si la versión nueva cambió el esquema:

```powershell
Copy-Item ".\fiadex-backup.db" "$env:LOCALAPPDATA\Fiadex\fiadex.db" -Force
```

4. Ejecuta el `Fiadex.exe` de la versión anterior y valida el checklist.

---

## 12. Solución de problemas de despliegue

| Síntoma | Causa probable | Acción |
|---|---|---|
| “No se reconoce Fiadex.exe” | ZIP incompleto o ejecutado desde dentro del archivo comprimido | Extrae todo el ZIP en una carpeta y ejecuta desde ahí. |
| Falta `e_sqlite3.dll` | Carpeta incompleta | Vuelve a extraer el ZIP completo; no copies solo el exe. |
| No se crea la base | Sin permisos en `%LOCALAPPDATA%` | Verifica permisos del perfil de Windows. |
| No aparecen los cambios nuevos | Se está ejecutando una versión antigua | Cierra todas las instancias y vuelve a extraer el ZIP actual. |
| Los duplicados desaparecieron al abrir | Migración automática `ConsolidateLoanAccounts` | Restaura `fiadex-backup.db` si necesitas los datos originales. |
| Ollama no responde | Servicio detenido o modelo ausente | Inicia Ollama y ejecuta `ollama pull qwen2.5:3b`. |
| El hash no coincide | ZIP generado antes del último cambio | Vuelve a ejecutar `publish.ps1` y repite la verificación. |
