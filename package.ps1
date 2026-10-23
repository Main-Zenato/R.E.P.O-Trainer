# Builds the release archives in build\release:
#   RepoTrainer-vX-full.zip    BepInEx 5 + trainer, extract straight into the game folder
#   RepoTrainer-vX-plugin.zip  the trainer alone, for an existing BepInEx 5 install
# Usage: powershell -ExecutionPolicy Bypass -File package.ps1
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$bepPkg = Join-Path $root ".tools\bepinex\pkg"
if (-not (Test-Path (Join-Path $bepPkg "winhttp.dll"))) { throw "Run install.ps1 once first: it downloads BepInEx into .tools." }

$dotnet = Join-Path $root ".tools\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = "dotnet" }
& $dotnet build (Join-Path $root "RepoTrainer.csproj") -c Release -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$version = (Select-String -Path (Join-Path $root "src\Plugin.cs") -Pattern 'Version = "([^"]+)"').Matches[0].Groups[1].Value
$out = Join-Path $root "build\release"
$stage = Join-Path $out "stage"
Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $stage | Out-Null

# Full package: BepInEx, its config tweak, and the plugin.
$full = Join-Path $stage "full"
Copy-Item $bepPkg $full -Recurse
New-Item -ItemType Directory -Force (Join-Path $full "BepInEx\config"), (Join-Path $full "BepInEx\plugins\RepoTrainer") | Out-Null
Set-Content (Join-Path $full "BepInEx\config\BepInEx.cfg") "[Chainloader]`r`n`r`nHideManagerGameObject = true`r`n" -Encoding UTF8
Copy-Item (Join-Path $root "build\RepoTrainer.dll") (Join-Path $full "BepInEx\plugins\RepoTrainer")
Compress-Archive (Get-ChildItem $full -Force).FullName (Join-Path $out "RepoTrainer-v$version-full.zip")

# Plugin only.
$plugin = Join-Path $stage "plugin"
New-Item -ItemType Directory -Force (Join-Path $plugin "BepInEx\plugins\RepoTrainer") | Out-Null
Copy-Item (Join-Path $root "build\RepoTrainer.dll") (Join-Path $plugin "BepInEx\plugins\RepoTrainer")
Compress-Archive (Join-Path $plugin "*") (Join-Path $out "RepoTrainer-v$version-plugin.zip")

Remove-Item $stage -Recurse -Force
Get-ChildItem $out | ForEach-Object { "{0}  {1:N0} KB" -f $_.Name, ($_.Length / 1KB) }
