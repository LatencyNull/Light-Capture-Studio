param (
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",

    [ValidateSet("win-x64", "win-x86")]
    [string]$Architecture = "win-x64",

    [string]$DeployPath = $null,

    [switch]$SelfContained = $true,
    [switch]$Clean,
    [switch]$Run
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Light Capture Studio - Build System  " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 1. Comprobar SDK de .NET
try {
    $dotnetVersion = (dotnet --version).Trim()
    Write-Host "[OK] SDK de .NET detectado: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Error "No se encontro el SDK de .NET en el sistema. Asegurate de tener .NET 8 SDK instalado en el PATH."
    exit 1
}

# 2. Detener instancias activas si existen
$running = Get-Process -Name "LightCapture", "LightCapture.Recorder" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "[!] Se detectaron instancias activas de Light Capture en ejecucion. Cerrando..." -ForegroundColor Yellow
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 800
}

# 3. Limpieza opcional
$publishDir = Join-Path $ScriptDir "publish\standalone"
if (-not $SelfContained) {
    $publishDir = Join-Path $ScriptDir "publish\framework-dependent"
}

if ($Clean) {
    Write-Host "[*] Limpiando artefactos previos..." -ForegroundColor Yellow
    dotnet clean LightCaptureStudio.sln -c $Configuration --nologo -v q
    $folders = @("publish", "release")
    foreach ($f in $folders) {
        $dir = Join-Path $ScriptDir $f
        if (Test-Path $dir) {
            Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    Write-Host "[OK] Limpieza completada." -ForegroundColor Green
}

# 4. Compilacion de la Solucion Completa
Write-Host "[*] Compilando solucion LightCaptureStudio.sln ($Configuration)..." -ForegroundColor Cyan
dotnet build LightCaptureStudio.sln -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Error "Error durante la compilacion de la solucion."
    exit $LASTEXITCODE
}
Write-Host "[OK] Compilacion correcta (0 advertencias, 0 errores)." -ForegroundColor Green

# 5. Publicacion Single-File Standalone
$modeText = "Autonoma Standalone (Self-Contained)"
if (-not $SelfContained) {
    $modeText = "Dependiente del Framework"
}
Write-Host "[*] Publicando ejecutable Single-File ($modeText)..." -ForegroundColor Cyan

$publishArgs = @(
    "publish",
    "src/LightCapture.App/LightCapture.App.csproj",
    "-c", $Configuration,
    "-r", $Architecture,
    "--nologo",
    "-o", $publishDir,
    "/p:PublishSingleFile=true",
    "/p:IncludeNativeLibrariesForSelfExtract=true",
    "/p:DebugType=none",
    "/p:DebugSymbols=false"
)

if ($SelfContained) {
    $publishArgs += "--self-contained"
    $publishArgs += "true"
    $publishArgs += "/p:EnableCompressionInSingleFile=true"
} else {
    $publishArgs += "--self-contained"
    $publishArgs += "false"
}

& dotnet @publishArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "Error durante la publicacion."
    exit $LASTEXITCODE
}

# Limpiar archivos auxiliares de depuracion para dejar un unico .EXE 100% limpio
Get-ChildItem -Path "$publishDir\*" -Include *.pdb, *.xml, *.json -File | Remove-Item -Force -ErrorAction SilentlyContinue

$exePath = Join-Path $publishDir "LightCapture.exe"
if (Test-Path $exePath) {
    $fileInfo = Get-Item $exePath
    $sizeMb = [math]::Round($fileInfo.Length / 1MB, 2)
    $hash = (Get-FileHash -Path $exePath -Algorithm SHA256).Hash

    # 6. Generar archivo comprimido .ZIP en la carpeta release/
    $releaseDir = Join-Path $ScriptDir "release"
    if (-not (Test-Path $releaseDir)) {
        New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
    }

    # Extraer version dinamicamente desde LightCapture.App.csproj
    $appVersion = "1.0.0"
    $csprojPath = Join-Path $ScriptDir "src\LightCapture.App\LightCapture.App.csproj"
    if (Test-Path $csprojPath) {
        $match = Select-String -Path $csprojPath -Pattern "<Version>(.*?)</Version>"
        if ($match -and $match.Matches.Groups.Count -gt 1) {
            $appVersion = $match.Matches.Groups[1].Value.Trim()
        }
    }

    $zipName = "LightCapture-Studio-Standalone-win-x64.zip"
    $releaseExeName = "LightCapture-Studio-v$appVersion-Standalone-win-x64.exe"
    if (-not $SelfContained) {
        $zipName = "LightCapture-Studio-FrameworkDependent-win-x64.zip"
        $releaseExeName = "LightCapture-Studio-v$appVersion-FrameworkDependent-win-x64.exe"
    }
    $zipPath = Join-Path $releaseDir $zipName
    $releaseExePath = Join-Path $releaseDir $releaseExeName

    # Copiar ejecutable directo nombrado
    Copy-Item -Path $exePath -Destination $releaseExePath -Force

    Write-Host "[*] Empaquetando archivo ZIP en $zipPath..." -ForegroundColor Cyan
    Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath -Force

    $zipInfo = Get-Item $zipPath
    $zipSizeMb = [math]::Round($zipInfo.Length / 1MB, 2)

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "   Publicacion completada con exito!    " -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "Ejecutable: $exePath ($sizeMb MB)"
    Write-Host "Archivo ZIP: $zipPath ($zipSizeMb MB)"
    Write-Host "Modo:        $modeText"
    Write-Host "SHA256:      $hash"
    Write-Host "========================================"
    Write-Host ""

    # Sincronizacion opcional con carpeta de despliegue si se especifica (-DeployPath)
    if ($DeployPath -and (Test-Path $DeployPath)) {
        try {
            Copy-Item -Path "$publishDir\*" -Destination $DeployPath -Recurse -Force
            Write-Host "[OK] Desplegado exitosamente en: $DeployPath" -ForegroundColor Green
        } catch {
            Write-Warning "No se pudo copiar en la ruta de despliegue: $($_.Exception.Message)"
        }
    }
} else {
    Write-Error "No se encontro el ejecutable generado en $exePath."
    exit 1
}

# 7. Ejecucion opcional
if ($Run) {
    Write-Host "[*] Iniciando Light Capture Studio..." -ForegroundColor Cyan
    Start-Process -FilePath $exePath
    Write-Host "[OK] Proceso iniciado." -ForegroundColor Green
}
