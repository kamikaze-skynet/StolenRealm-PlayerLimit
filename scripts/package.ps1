# Builds the mod and writes the Thunderstore zip to dist\.
# Set STOLEN_REALM_DIR and BEPINEX_CORE first (see README).
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content (Join-Path $root "package\manifest.json") -Raw | ConvertFrom-Json
$version = $manifest.version_number

dotnet build (Join-Path $root "PlayerLimitMod") -c Release -o (Join-Path $root "PlayerLimitMod\bin\Release")
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$stage = Join-Path $root "dist\stage"
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item (Join-Path $root "package\*") $stage
Copy-Item (Join-Path $root "PlayerLimitMod\bin\Release\PlayerLimitMod.dll") $stage

$zip = Join-Path $root "dist\girthy_mods-Player_Limit_Mod-$version.zip"
Remove-Item $zip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip
Remove-Item $stage -Recurse -Force
Write-Host "Created $zip"
