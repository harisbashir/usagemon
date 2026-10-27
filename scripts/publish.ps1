param([string]$Runtime = 'win-x64', [switch]$FrameworkDependent)
$ErrorActionPreference = 'Stop'
if ($Runtime -notin @('win-x64', 'win-arm64')) { throw 'Runtime must be win-x64 or win-arm64.' }
$root = Split-Path $PSScriptRoot -Parent
$suffix = if ($FrameworkDependent) { '-framework-dependent' } else { '' }
$destination = Join-Path $root "artifacts/UsageMon-$Runtime$suffix"
if (Test-Path $destination) { Remove-Item $destination -Recurse -Force }
$compression = if ($FrameworkDependent) { 'false' } else { 'true' }
dotnet publish (Join-Path $root 'src/UsageMon.App/UsageMon.App.csproj') -c Release -r $Runtime --self-contained (-not $FrameworkDependent) -p:PublishSingleFile=true "-p:EnableCompressionInSingleFile=$compression" -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $destination
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Copy-Item (Join-Path $root 'README.md') $destination
Copy-Item (Join-Path $root 'LICENSE') $destination
$zip = Join-Path $root "artifacts/UsageMon-$Runtime$suffix.zip"
Compress-Archive -Path "$destination/*" -DestinationPath $zip -Force
$executable = Join-Path $root "artifacts/UsageMon-$Runtime$suffix.exe"
Copy-Item (Join-Path $destination 'UsageMon.exe') $executable -Force
Write-Host "Portable build: $zip"
Write-Host "Standalone executable: $executable"
