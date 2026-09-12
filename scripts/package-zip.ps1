<#
.SYNOPSIS
    Publishes YukiMcp and zips it up together with the README, ready to hand to someone else.

.DESCRIPTION
    Runs publish.ps1 for the given runtime, then bundles the published exe with README.md and
    LICENSE into a single zip under dist/. That zip is the thing you send: the recipient unzips
    it, points Claude Desktop's config at the exe inside, and follows the README. See
    ../plan.md ("Distributie & installatie").

.PARAMETER Runtime
    A .NET runtime identifier. Defaults to win-x64.

.PARAMETER Version
    Version string used in the zip file name (e.g. "0.1.0"). Defaults to "dev".

.EXAMPLE
    ./scripts/package-zip.ps1
    ./scripts/package-zip.ps1 -Runtime win-x64 -Version 0.1.0
#>
param(
    [string]$Runtime = "win-x64",
    [string]$Version = "dev"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot

& (Join-Path $PSScriptRoot "publish.ps1") -Runtime $Runtime

$PublishDir = Join-Path $RepoRoot "dist/$Runtime"
$StagingDir = Join-Path $RepoRoot "dist/staging-$Runtime"
$ZipPath = Join-Path $RepoRoot "dist/YukiMcp-$Version-$Runtime.zip"

if (Test-Path $StagingDir) { Remove-Item $StagingDir -Recurse -Force }
New-Item -ItemType Directory -Path $StagingDir | Out-Null

Copy-Item (Join-Path $PublishDir "*") $StagingDir -Recurse
Copy-Item (Join-Path $RepoRoot "README.md") $StagingDir
Copy-Item (Join-Path $RepoRoot "LICENSE") $StagingDir

if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
Compress-Archive -Path (Join-Path $StagingDir "*") -DestinationPath $ZipPath

Remove-Item $StagingDir -Recurse -Force

Write-Host "Created $ZipPath"
