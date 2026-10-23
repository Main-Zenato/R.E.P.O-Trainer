# Builds the trainer and installs it (with BepInEx 5 if missing) into R.E.P.O.
# Usage: powershell -ExecutionPolicy Bypass -File install.ps1 [-GameDir "D:\...\REPO"] [-NoBuild]
param(
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\REPO",
    [switch]$NoBuild
)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$bepVersion = "5.4.23.5"
$bepDir = Join-Path $root ".tools\bepinex"
$bepPkg = Join-Path $bepDir "pkg"

if (-not (Test-Path (Join-Path $GameDir "REPO.exe"))) {
    throw "Game not found in '$GameDir'. Pass the right folder with -GameDir."
}
if (Get-Process -Name "REPO" -ErrorAction SilentlyContinue) {
    throw "Close the game before installing the trainer."
}

if (-not (Test-Path (Join-Path $bepPkg "winhttp.dll"))) {
    Write-Host "Downloading BepInEx $bepVersion..."
    New-Item -ItemType Directory -Force $bepDir | Out-Null
    $zip = Join-Path $bepDir "bep.zip"
    Invoke-WebRequest "https://github.com/BepInEx/BepInEx/releases/download/v$bepVersion/BepInEx_win_x64_$bepVersion.zip" -OutFile $zip
    Expand-Archive $zip -DestinationPath $bepPkg -Force
}

if (-not $NoBuild) {
    $dotnet = Join-Path $root ".tools\dotnet\dotnet.exe"
    if (-not (Test-Path $dotnet)) { $dotnet = "dotnet" }
    & $dotnet build (Join-Path $root "RepoTrainer.csproj") -c Release -nologo -v q "-p:GameDir=$GameDir"
    if ($LASTEXITCODE -ne 0) { throw "Build failed." }
}

if (-not (Test-Path (Join-Path $GameDir "BepInEx\core\BepInEx.dll"))) {
    Write-Host "Installing BepInEx into the game..."
    Copy-Item (Join-Path $bepPkg "*") $GameDir -Recurse -Force
    Copy-Item (Join-Path $bepPkg ".doorstop_version") $GameDir -Force
}

# Some Unity games destroy BepInEx's manager object on scene load; hiding it keeps plugins alive.
$cfgDir = Join-Path $GameDir "BepInEx\config"
$cfg = Join-Path $cfgDir "BepInEx.cfg"
New-Item -ItemType Directory -Force $cfgDir | Out-Null
if (-not (Test-Path $cfg)) {
    Set-Content $cfg "[Chainloader]`r`n`r`nHideManagerGameObject = true`r`n" -Encoding UTF8
} elseif ((Get-Content $cfg -Raw) -match "HideManagerGameObject = false") {
    (Get-Content $cfg -Raw) -replace "HideManagerGameObject = false", "HideManagerGameObject = true" | Set-Content $cfg -Encoding UTF8
}

$pluginDir = Join-Path $GameDir "BepInEx\plugins\RepoTrainer"
New-Item -ItemType Directory -Force $pluginDir | Out-Null
Copy-Item (Join-Path $root "build\RepoTrainer.dll") $pluginDir -Force

Write-Host "Trainer installed in $pluginDir"
Write-Host "Start the game and press Insert to open the menu."
