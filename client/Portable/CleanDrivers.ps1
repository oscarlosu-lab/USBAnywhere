# Script para limpiar drivers USB/IP
# Ejecutar como Administrador

Write-Host "=== Limpieza de Drivers USB/IP ===" -ForegroundColor Cyan

# [0] Eliminar nodos de dispositivo virtuales USB/IP (UDE / VHCI)
Write-Host "`n[0] Removiendo nodos de dispositivo virtuales USB/IP..." -ForegroundColor Yellow
$devnodeExe = Join-Path $PSScriptRoot "devnode.exe"
if (Test-Path $devnodeExe) {
    & $devnodeExe remove "ROOT\USBIP_WIN2\UDE" root 2>&1 | Out-Null
    Write-Host "  Ejecutado devnode.exe remove ROOT\USBIP_WIN2\UDE root" -ForegroundColor Gray
}

$deviceOutput = pnputil /enum-devices /class USB 2>&1
$deviceBlocks = ($deviceOutput -join "`n") -split "(?=Id\. de instancia:|Instance ID:)"

# Buscar exclusivamente instancias ROOT\USB\000x que pertenezcan a USBip (ignorando Parsec u otros adaptadores virtuales)
$usbipDevices = @()
foreach ($block in $deviceBlocks) {
    if ($block -match "USBip|USBIP_WIN2" -and $block -match '(ROOT\\USB\\\d{4}|ROOT\\USBIP_WIN2\\\S+)') {
        $deviceId = $matches[1]
        if ($deviceId -notin $usbipDevices) {
            $usbipDevices += $deviceId
        }
    }
}

if ($usbipDevices.Count -gt 0) {
    Write-Host "  Encontradas $($usbipDevices.Count) instancia(s) de USBip Host Controller" -ForegroundColor DarkYellow
    foreach ($deviceId in $usbipDevices) {
        Write-Host "    Eliminando: $deviceId" -ForegroundColor Gray
        $result = pnputil /remove-device "$deviceId" 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "      OK: Dispositivo eliminado" -ForegroundColor Green
        } else {
            Write-Host "      ADVERTENCIA: No se pudo eliminar (puede requerir reinicio)" -ForegroundColor DarkYellow
        }
    }
} else {
    Write-Host "  No se encontraron nodos de dispositivo USB/IP activos" -ForegroundColor Green
}

# Buscar drivers USB/IP instalados
Write-Host "`n[1] Buscando drivers USB/IP instalados..." -ForegroundColor Yellow
$driverList = @()
$output = pnputil /enum-drivers
$blocks = $output -join "`n" -split "(?=Nombre publicado:|Published name:)"

foreach ($block in $blocks) {
    if ($block -match "usbip|vhci" -and $block -match "(oem\d+\.inf)") {
        $oemName = $matches[1]
        $driverList += $oemName
        Write-Host "  Encontrado: $oemName" -ForegroundColor White
    }
}

if ($driverList.Count -eq 0) {
    Write-Host "  No se encontraron drivers USB/IP instalados." -ForegroundColor Green
    Write-Host "`nPresiona Enter para salir..."
    Read-Host
    exit
}

# Detener servicios relacionados
Write-Host "`n[2] Deteniendo servicios USB/IP..." -ForegroundColor Yellow
$services = @("usbip2_ude", "usbip2_filter", "vhci_ude", "usbip_vhci")
foreach ($svc in $services) {
    try {
        $svcObj = Get-Service -Name $svc -ErrorAction SilentlyContinue
        if ($svcObj) {
            Stop-Service -Name $svc -Force -ErrorAction SilentlyContinue
            Write-Host "  Servicio detenido: $svc" -ForegroundColor White
        }
    }
    catch { }
}

# Eliminar drivers con pnputil /uninstall
Write-Host "`n[3] Eliminando drivers del sistema..." -ForegroundColor Yellow
foreach ($driver in $driverList) {
    Write-Host "  Eliminando $driver..." -ForegroundColor White
    $result = pnputil /delete-driver $driver /uninstall 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "    OK: Driver eliminado" -ForegroundColor Green
    }
    else {
        # Intentar sin /uninstall si falla
        $result = pnputil /delete-driver $driver /force 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "    OK: Driver eliminado (forzado)" -ForegroundColor Green
        }
        else {
            Write-Host "    ADVERTENCIA: No se pudo eliminar completamente. Puede requerir reinicio." -ForegroundColor DarkYellow
        }
    }
}

# Verificar resultado
Write-Host "`n[4] Verificando drivers restantes..." -ForegroundColor Yellow
$remainingOutput = pnputil /enum-drivers
$remainingBlocks = $remainingOutput -join "`n" -split "(?=Nombre publicado:|Published name:)"
$remaining = 0
foreach ($block in $remainingBlocks) {
    if ($block -match "usbip|vhci" -and $block -match "(oem\d+\.inf)") {
        $remaining++
        Write-Host "  Aun instalado: $($matches[1])" -ForegroundColor DarkYellow
    }
}

if ($remaining -eq 0) {
    Write-Host "`n=== LIMPIEZA COMPLETADA ===" -ForegroundColor Green
    Write-Host "Todos los drivers USB/IP han sido eliminados."
}
else {
    Write-Host "`n=== LIMPIEZA PARCIAL ===" -ForegroundColor Yellow
    Write-Host "Algunos drivers no pudieron eliminarse."
    Write-Host "Reinicia el PC e intenta ejecutar este script de nuevo."
}

Write-Host "`nPresiona Enter para salir..."
Read-Host
