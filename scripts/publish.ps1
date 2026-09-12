<#
.SYNOPSIS
    Publishes YukiMcp as a single, self-contained executable for one runtime.

.DESCRIPTION
    Produces the artifact that gets handed to end users: one .exe (plus a .pdb) with the
    .NET runtime baked in, so nothing needs to be installed on the target machine beyond the
    file itself. See ../plan.md ("Distributie & installatie") and ../README.md.

.PARAMETER Runtime
    A .NET runtime identifier. Defaults to win-x64, since Claude Desktop's primary target here
    is Windows. Other supported values (see src/YukiMcp/YukiMcp.csproj RuntimeIdentifiers):
    osx-x64, osx-arm64, linux-x64.

.EXAMPLE
    ./scripts/publish.ps1
    ./scripts/publish.ps1 -Runtime osx-arm64
#>
param(
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ProjectPath = Join-Path $RepoRoot "src/YukiMcp/YukiMcp.csproj"
$OutDir = Join-Path $RepoRoot "dist/$Runtime"

Write-Host "Publishing YukiMcp for $Runtime -> $OutDir"

dotnet publish $ProjectPath `
    -c Release `
    -r $Runtime `
    -o $OutDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Write-Host "Done: $OutDir"
