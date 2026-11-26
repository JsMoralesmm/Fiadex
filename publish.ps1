$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "Fiadex\Fiadex.csproj"
$output = Join-Path $root "publish\win-x64"
$zip = Join-Path $root "Fiadex-win-x64-portable.zip"

dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $output

# Incluir los manuales en PDF dentro del paquete portable, si ya fueron generados.
$manuals = Join-Path $root "Manuales_PDF"
$manualDestination = Join-Path $output "Manuales"
if (Test-Path -LiteralPath $manuals) {
    if (-not (Test-Path -LiteralPath $manualDestination)) {
        New-Item -ItemType Directory -Path $manualDestination -Force | Out-Null
    }
    Copy-Item -Path (Join-Path $manuals "*.pdf") -Destination $manualDestination -Force
}

# No distribuir símbolos de depuración: facilitan ingeniería inversa y no aportan al usuario.
Get-ChildItem -LiteralPath $output -Filter "*.pdb" -Recurse -File |
    Remove-Item -Force -ErrorAction SilentlyContinue

if (Test-Path -LiteralPath $zip) {
    Remove-Item -LiteralPath $zip -Force
}

# Comprimir desde una copia temporal: así un Fiadex.exe abierto no bloquea
# los DLL de publish\win-x64 durante la compresión.
$staging = Join-Path $root "publish\_staging"
if (Test-Path -LiteralPath $staging) {
    Remove-Item -LiteralPath $staging -Recurse -Force
}
New-Item -ItemType Directory -Path $staging -Force | Out-Null
Copy-Item -Path (Join-Path $output "*") -Destination $staging -Recurse -Force

try {
    Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $zip
} finally {
    Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Paquete portable generado: $zip"
