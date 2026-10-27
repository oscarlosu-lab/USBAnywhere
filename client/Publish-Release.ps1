# Publish-Release.ps1

# Automates the release process for USBAnywhere



$version = "3.0.1"

$repoRoot = $PSScriptRoot

$projectPath = Join-Path $repoRoot "src\USBAnywhere"

$releasePath = Join-Path $repoRoot "release"

$usbIdsPath = Join-Path $projectPath "usb.ids"



# New folder structure (root level)

$portableX64 = "$repoRoot\Portable-X64"

$portableArm64 = "$repoRoot\Portable-ARM64"



# Inno Setup Compiler Path

$iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"

if (-not (Test-Path $iscc)) { $iscc = "${env:ProgramFiles}\Inno Setup 6\ISCC.exe" }



Write-Host "Starting USBAnywhere v$version Release Build..." -ForegroundColor Cyan



# Clean release folder

if (Test-Path $releasePath) { Remove-Item "$releasePath\*.zip", "$releasePath\*.exe", "$releasePath\SHA256SUMS.txt" -Force -ErrorAction SilentlyContinue }

New-Item -ItemType Directory -Path "$releasePath\temp_x64\Portable" | Out-Null

New-Item -ItemType Directory -Path "$releasePath\temp_arm64\Portable" | Out-Null



# ---------------------------------------------------------

# 1. Build Executables

# ---------------------------------------------------------



Write-Host "Building x64..." -ForegroundColor Yellow

dotnet publish "$projectPath\USBAnywhere.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "$releasePath\temp_x64\Portable" --nologo

if ($LASTEXITCODE -ne 0) { Write-Error "Build x64 failed"; exit 1 }



Write-Host "Building ARM64..." -ForegroundColor Yellow

dotnet publish "$projectPath\USBAnywhere.csproj" -c Release -r win-arm64 --self-contained true -p:PublishSingleFile=true -o "$releasePath\temp_arm64\Portable" --nologo

if ($LASTEXITCODE -ne 0) { Write-Error "Build ARM64 failed"; exit 1 }



# ---------------------------------------------------------

# 2. Prepare Assets

# ---------------------------------------------------------



Write-Host "Copying assets..." -ForegroundColor Cyan



# Copy usb.ids

if (Test-Path $usbIdsPath) {

    Copy-Item $usbIdsPath "$releasePath\temp_x64\Portable\usb.ids" -Force

    Copy-Item $usbIdsPath "$releasePath\temp_arm64\Portable\usb.ids" -Force

}



# Helper tools

$helpers = @("usbipw.exe", "devnode.exe", "libusbip.dll", "resources.dll", "CleanDrivers.ps1", "Logo-SnakeFoxU-con-e.ico")

foreach ($file in $helpers) {

    $src = "$repoRoot\Portable\$file"

    if (Test-Path $src) {

        Copy-Item $src "$releasePath\temp_x64\Portable\$file" -Force

    }

    $armSrc = "$portableArm64\$file"

    if (Test-Path $armSrc) {

        Copy-Item $armSrc "$releasePath\temp_arm64\Portable\$file" -Force

    } else {

        throw "FATAL: Missing ARM64 helper binary: $armSrc. Cannot produce valid ARM64 package."

    }

}



# Drivers (Architecture-specific: WHQL certified)

$x64DriversPath = "$repoRoot\Portable\drivers"

if (Test-Path $x64DriversPath) {

    Copy-Item $x64DriversPath "$releasePath\temp_x64\Portable\drivers" -Recurse -Force

}



$arm64DriversPath = "$portableArm64\drivers"

if (Test-Path $arm64DriversPath) {

    Copy-Item $arm64DriversPath "$releasePath\temp_arm64\Portable\drivers" -Recurse -Force

} else {

    throw "FATAL: Missing ARM64 drivers: $arm64DriversPath. Cannot produce valid ARM64 package."

}



# License and Readme
$licenseFile = if (Test-Path "$repoRoot\LICENSE") { "$repoRoot\LICENSE" } else { "$repoRoot\..\LICENSE" }
$readmeFile = if (Test-Path "$repoRoot\README.md") { "$repoRoot\README.md" } else { "$repoRoot\..\README.md" }

Copy-Item $licenseFile "$releasePath\temp_x64\Portable\LICENSE" -Force

Copy-Item $readmeFile "$releasePath\temp_x64\Portable\README.md" -Force

Copy-Item $licenseFile "$releasePath\temp_arm64\Portable\LICENSE" -Force

Copy-Item $readmeFile "$releasePath\temp_arm64\Portable\README.md" -Force



# ARM64 specific README (test-signed driver warning)

$arm64Readme = "$portableArm64\README_ARM64.md"

if (Test-Path $arm64Readme) {

    Copy-Item $arm64Readme "$releasePath\temp_arm64\Portable\README_ARM64.md" -Force

}



# Wizard images for installer (copy to temp_x64 root for ISCC)

$wizardImages = @("wizard_image.bmp", "wizard_small.bmp")

foreach ($img in $wizardImages) {

    $src = "$repoRoot\_reference\$img"

    if (Test-Path $src) { Copy-Item $src "$releasePath\temp_x64\$img" -Force }

}



# Icon for installer

$iconSrc = "$projectPath\Assets\icon.ico"

if (Test-Path $iconSrc) { Copy-Item $iconSrc "$releasePath\temp_x64\icon.ico" -Force }



# ---------------------------------------------------------

# 3. Clean unnecessary files from Portable folders

# ---------------------------------------------------------



Write-Host "Cleaning unnecessary files..." -ForegroundColor Gray



# Remove .pdb files (debug symbols)

Get-ChildItem "$releasePath\temp_x64\Portable" -Filter "*.pdb" | Remove-Item -Force

Get-ChildItem "$releasePath\temp_arm64\Portable" -Filter "*.pdb" | Remove-Item -Force



# Remove Assets folder (generated by .NET)

$assetsX64 = "$releasePath\temp_x64\Portable\Assets"

$assetsArm64 = "$releasePath\temp_arm64\Portable\Assets"

if (Test-Path $assetsX64) { Remove-Item $assetsX64 -Recurse -Force }

if (Test-Path $assetsArm64) { Remove-Item $assetsArm64 -Recurse -Force }



# ---------------------------------------------------------

# 4. Create ZIP Packages

# ---------------------------------------------------------



Write-Host "Creating ZIP packages..." -ForegroundColor Magenta

$x64Zip = "$releasePath\USBAnywhere-v$version-x64.zip"

$arm64Zip = "$releasePath\USBAnywhere-v$version-arm64.zip"



# Create portable markers for ZIP releases (Hybrid Mode)

New-Item -Path "$releasePath\temp_x64\Portable\.portable" -ItemType File -Force | Out-Null

New-Item -Path "$releasePath\temp_x64\Portable\portable.flag" -ItemType File -Force | Out-Null

New-Item -Path "$releasePath\temp_arm64\Portable\.portable" -ItemType File -Force | Out-Null

New-Item -Path "$releasePath\temp_arm64\Portable\portable.flag" -ItemType File -Force | Out-Null



Compress-Archive -Path "$releasePath\temp_x64\Portable\*" -DestinationPath $x64Zip -Force

Compress-Archive -Path "$releasePath\temp_arm64\Portable\*" -DestinationPath $arm64Zip -Force



# ---------------------------------------------------------

# 5. Create Setup Installer (x64 only)

# ---------------------------------------------------------



if (Test-Path $iscc) {

    Write-Host "Compiling Setup (x64)..." -ForegroundColor Yellow

    

    # Remove portable markers for installer (uses AppData instead)

    Remove-Item "$releasePath\temp_x64\Portable\.portable", "$releasePath\temp_x64\Portable\portable.flag" -Force -ErrorAction SilentlyContinue

    

    # Copy LICENSE and README to temp_x64 root for ISCC

    Copy-Item $licenseFile "$releasePath\temp_x64\LICENSE" -Force

    Copy-Item $readmeFile "$releasePath\temp_x64\README.md" -Force

    

    $issSrc = "$repoRoot\installer\USBAnywhere.iss"

    if (-not (Test-Path $issSrc)) { $issSrc = "$repoRoot\installer\USBAnywhere.iss" }

    $issDest = "$releasePath\temp_x64\Setup.iss"

    

    $issContent = Get-Content $issSrc -Raw

    $issContent = $issContent -replace '#define MyAppVersion ".*?"', "#define MyAppVersion ""$version"""

    $issContent = $issContent -replace 'OutputDir=installer', "OutputDir=$releasePath"

    $issContent = $issContent -replace 'LicenseFile=LICENSE', 'LicenseFile=./LICENSE'

    $issContent = $issContent -replace 'WizardImageFile=wizard_image.bmp', 'WizardImageFile=./wizard_image.bmp'

    $issContent = $issContent -replace 'WizardSmallImageFile=wizard_small.bmp', 'WizardSmallImageFile=./wizard_small.bmp'

    

    Set-Content -Path $issDest -Value $issContent

    

    $proc = Start-Process -FilePath $iscc -ArgumentList "`"$issDest`"" -WorkingDirectory "$releasePath\temp_x64" -PassThru -NoNewWindow -Wait

    

    if ($proc.ExitCode -eq 0) { Write-Host "Setup (x64) created successfully" -ForegroundColor Green }

    else { Write-Error "Setup failed (Exit: $($proc.ExitCode))" }

}

else {

    Write-Warning "ISCC.exe not found. Skipping Installer."

}



# Clean temp build directories

Write-Host "Cleaning temporary folders..." -ForegroundColor Gray

Remove-Item "$releasePath\temp_x64", "$releasePath\temp_arm64" -Recurse -Force -ErrorAction SilentlyContinue



# ---------------------------------------------------------

# 5b. SHA256SUMS (required by the in-app updater, fail-closed)

# ---------------------------------------------------------



Write-Host "Generating SHA256SUMS.txt..." -ForegroundColor Cyan

$sumsFile = "$releasePath\SHA256SUMS.txt"

Get-ChildItem $releasePath -File | Where-Object { $_.Extension -in '.zip', '.exe' } | ForEach-Object {

    $h = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()

    "$h  $($_.Name)"

} | Set-Content -Path $sumsFile -Encoding ASCII

Get-Content $sumsFile | ForEach-Object { Write-Host "   $_" }



# ---------------------------------------------------------

# 6. Summary

# ---------------------------------------------------------



Write-Host ""

Write-Host "Release Build Complete!" -ForegroundColor Green

Write-Host "   ZIP x64:    $x64Zip"

Write-Host "   ZIP ARM64:  $arm64Zip"

Write-Host "   Setup x64:  $releasePath\USBAnywhere_Setup_v$version.exe"

