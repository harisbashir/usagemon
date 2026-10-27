param([string]$Runtime = 'win-x64')
$ErrorActionPreference = 'Stop'
if ($Runtime -notin @('win-x64', 'win-arm64')) { throw 'Runtime must be win-x64 or win-arm64.' }
$root = Split-Path $PSScriptRoot -Parent
$destination = Join-Path $root "artifacts/UsageMon-$Runtime"
if (Test-Path $destination) { Remove-Item $destination -Recurse -Force }
dotnet publish (Join-Path $root 'src/UsageMon.App/UsageMon.App.csproj') -c Release -r $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $destination
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Copy-Item (Join-Path $root 'README.md') $destination
Copy-Item (Join-Path $root 'LICENSE') $destination
$zip = Join-Path $root "artifacts/UsageMon-$Runtime.zip"
Compress-Archive -Path "$destination/*" -DestinationPath $zip -Force
Write-Host "Portable build: $zip"
