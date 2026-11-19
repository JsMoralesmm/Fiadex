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

if (Test-Path -LiteralPath $zip) {
    Remove-Item -LiteralPath $zip -Force
}

Compress-Archive -Path (Join-Path $output "*") -DestinationPath $zip
Write-Host "Paquete portable generado: $zip"
