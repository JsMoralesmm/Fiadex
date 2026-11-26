#Requires -Version 7.0
<#
.SYNOPSIS
    Restablece la contraseña de la cuenta administradora de Fiadex sin borrar los datos.

.DESCRIPTION
    Herramienta de rescate para el administrador del equipo, no para el usuario final.
    Debe ejecutarse con Fiadex cerrado. Genera el mismo formato de hash que la
    aplicación (PBKDF2-SHA-256, 600.000 iteraciones, prefijo F2$) y deja la cuenta
    desbloqueada. Hace una copia de seguridad antes de modificar la base.

.EXAMPLE
    pwsh -File .\Herramientas\RestablecerContrasena.ps1

.EXAMPLE
    pwsh -File .\Herramientas\RestablecerContrasena.ps1 -NewPassword "ClaveNueva123!" -Force

.EXAMPLE
    pwsh -File .\Herramientas\RestablecerContrasena.ps1 -DatabasePath "C:\temp\fiadex.db"
#>
[CmdletBinding()]
param(
    [string] $DatabasePath,
    [string] $NewPassword,
    [switch] $Force
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root 'publish\win-x64'
$sqliteAssembly = Join-Path $publish 'Microsoft.Data.Sqlite.dll'

if (-not (Test-Path -LiteralPath $sqliteAssembly)) {
    throw "No se encontró Microsoft.Data.Sqlite.dll en: $publish. Ejecute primero publish.ps1."
}

$customPath = -not [string]::IsNullOrWhiteSpace($DatabasePath)

if (-not $customPath -and @(Get-Process -Name Fiadex -ErrorAction SilentlyContinue).Count -gt 0) {
    throw 'Fiadex está abierto. Cierre la aplicación antes de restablecer la contraseña.'
}

if (-not $customPath) {
    $DatabasePath = Join-Path $env:LOCALAPPDATA 'Fiadex\fiadex.db'
}
if (-not (Test-Path -LiteralPath $DatabasePath)) {
    throw "No se encontró la base de datos: $DatabasePath"
}

if ([string]::IsNullOrEmpty($NewPassword)) {
    $secret = Read-Host 'Nueva contraseña' -AsSecureString
    $NewPassword = [System.Net.NetworkCredential]::new('', $secret).Password
}

if ($NewPassword.Length -lt 12 -or
    -not ($NewPassword -cmatch '[A-Z]') -or
    -not ($NewPassword -cmatch '[a-z]') -or
    -not ($NewPassword -cmatch '[0-9]') -or
    -not ($NewPassword -cmatch '[^A-Za-z0-9]')) {
    throw 'La contraseña debe tener 12 caracteres, mayúscula, minúscula, número y símbolo.'
}

if (-not $Force) {
    $answer = Read-Host 'Esto cambiará la contraseña y desbloqueará la cuenta. Escriba SI para continuar'
    if ($answer -ne 'SI') {
        Write-Host 'Operación cancelada.'
        return
    }
}

# Respaldo antes de tocar la base (incluye los archivos WAL/SHM si existen).
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backup = "$DatabasePath.recuperacion-$stamp.bak"
Copy-Item -LiteralPath $DatabasePath -Destination $backup -Force
foreach ($suffix in @('-wal', '-shm')) {
    if (Test-Path -LiteralPath "$DatabasePath$suffix") {
        Copy-Item -LiteralPath "$DatabasePath$suffix" -Destination "$backup$suffix" -Force
    }
}

# Mismo esquema que Fiadex/PasswordSecurity.cs.
$saltBytes = [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(16)
$hashBytes = [System.Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2(
    $NewPassword,
    $saltBytes,
    600000,
    [System.Security.Cryptography.HashAlgorithmName]::SHA256,
    32)
$hashText = 'F2$' + [Convert]::ToBase64String($hashBytes)
$saltText = [Convert]::ToBase64String($saltBytes)

# Microsoft.Data.Sqlite está compilado para .NET: precargar sus dependencias
# desde la carpeta publicada antes de usar los tipos.
foreach ($dependency in @(
        'SQLitePCLRaw.core.dll',
        'SQLitePCLRaw.provider.e_sqlite3.dll',
        'SQLitePCLRaw.batteries_v2.dll')) {
    $dependencyPath = Join-Path $publish $dependency
    if (-not (Test-Path -LiteralPath $dependencyPath)) {
        throw "Falta $dependency en $publish"
    }
    $null = [System.Reflection.Assembly]::LoadFrom($dependencyPath)
}
$null = [System.Reflection.Assembly]::LoadFrom($sqliteAssembly)

$connection = [Microsoft.Data.Sqlite.SqliteConnection]::new(
    "Data Source=$DatabasePath;Mode=ReadWrite")
try {
    $connection.Open()

    $command = $connection.CreateCommand()
    $command.CommandText = @'
UPDATE Users
SET PasswordHash = $hash,
    PasswordSalt = $salt,
    FailedAttempts = 0,
    IsLocked = 0
'@
    $null = $command.Parameters.AddWithValue('$hash', $hashText)
    $null = $command.Parameters.AddWithValue('$salt', $saltText)
    $rows = $command.ExecuteNonQuery()
    if ($rows -ne 1) {
        throw "Se esperaba actualizar 1 cuenta, pero se actualizaron $rows."
    }

    # Confirmar que la base quedó como la aplicación la espera.
    $check = $connection.CreateCommand()
    $check.CommandText = @'
SELECT PasswordHash, PasswordSalt, IsLocked, FailedAttempts FROM Users LIMIT 1
'@
    $reader = $check.ExecuteReader()
    try {
        if (-not $reader.Read()) { throw 'No se pudo leer la cuenta después del cambio.' }
        $storedHash = [string] $reader.GetString(0)
        $storedSalt = [string] $reader.GetString(1)
        $locked = [long] $reader.GetInt64(2)
        $failed = [long] $reader.GetInt64(3)
    } finally {
        $reader.Dispose()
    }

    if ($storedHash -ne $hashText -or $storedSalt -ne $saltText -or $locked -ne 0 -or $failed -ne 0) {
        throw 'La verificación posterior al cambio falló; no use la contraseña nueva.'
    }
} finally {
    $connection.Dispose()
}

Write-Host 'Contraseña restablecida y cuenta desbloqueada. Los datos se conservaron.'
Write-Host "Respaldo: $backup"
Write-Host 'Ya puede iniciar sesión con la contraseña nueva.'
