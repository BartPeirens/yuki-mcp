<#
.SYNOPSIS
    Publishes YukiMcp and zips it up together with the README, ready to hand to someone else.

.DESCRIPTION
    Runs publish.ps1 (produces build/YukiMcp.exe), then bundles that exe with README.md, LICENSE,
    CHANGELOG.md and a ".env" template (APIKEY=Replace with apikey) into a single zip under dist/.
    That zip is the thing you send: the recipient unzips it, fills in their real key in .env, and
    points Claude Desktop's config at the exe inside - no API key ever goes in the Claude Desktop
    config itself. See ../plan.md ("Distributie & installatie").

    Versioning is a plain incrementing counter (v1, v2, ...), not SemVer - it tracks the
    distributed zip, not the Yuki tools' functionality. The next version number is read from
    ../VERSION (0 if that file doesn't exist yet, so the very first run produces v1) and written
    back after a successful build. Before bumping, the script checks that ../CHANGELOG.md's
    newest "## vN" section is for that new version - add one before packaging, or the script
    stops with an error telling you so. CHANGELOG.md is also copied into dist/ and into the zip
    itself, so the version history travels with the build.

.PARAMETER Runtime
    A .NET runtime identifier, forwarded to publish.ps1. Defaults to win-x64.

.PARAMETER Version
    Overrides the auto-incremented version number (e.g. -Version 3 to rebuild v3). Omit it to
    just take (last version in ../VERSION) + 1.

.EXAMPLE
    ./scripts/package-zip.ps1
    ./scripts/package-zip.ps1 -Runtime win-x64 -Version 3
#>
param(
    [string]$Runtime = "win-x64",
    [int]$Version = 0
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$VersionFile = Join-Path $RepoRoot "VERSION"
$ChangelogFile = Join-Path $RepoRoot "CHANGELOG.md"

$previousVersion = 0
if (Test-Path $VersionFile) {
    $previousVersion = [int](Get-Content $VersionFile -Raw).Trim()
}
if ($Version -le 0) {
    $Version = $previousVersion + 1
}
$VersionTag = "v$Version"

# Require CHANGELOG.md's newest entry to already describe this version, so no zip ever ships
# without a paper trail of what's in it.
$changelogHeading = Get-Content $ChangelogFile | Where-Object { $_ -match '^## v\d+' } | Select-Object -First 1
if (-not $changelogHeading -or $changelogHeading -notmatch "^## $VersionTag\b") {
    throw "CHANGELOG.md's newest entry ('$changelogHeading') is not for $VersionTag. Add a " +
        "'## $VersionTag - <date>' section to CHANGELOG.md describing what changed, then re-run."
}

& (Join-Path $PSScriptRoot "publish.ps1") -Runtime $Runtime

$BuildDir = Join-Path $RepoRoot "build"
$DistDir = Join-Path $RepoRoot "dist"
$StagingDir = Join-Path $DistDir "staging"
$ZipPath = Join-Path $DistDir "YukiMcp-$VersionTag-$Runtime.zip"

if (-not (Test-Path $DistDir)) { New-Item -ItemType Directory -Path $DistDir | Out-Null }
if (Test-Path $StagingDir) { Remove-Item $StagingDir -Recurse -Force }
New-Item -ItemType Directory -Path $StagingDir | Out-Null

# The exe + README + CHANGELOG + a .env template go in - that's everything someone needs to point
# Claude Desktop at it. The .env ships with a placeholder, never a real key: YukiServerOptions
# reads ".env" from the exe's own folder at startup, so the recipient just edits that file in
# place instead of putting the API key in claude_desktop_config.json.
Copy-Item (Join-Path $BuildDir "YukiMcp.exe") $StagingDir
Copy-Item (Join-Path $RepoRoot "README.md") $StagingDir
Copy-Item (Join-Path $RepoRoot "LICENSE") $StagingDir
Copy-Item $ChangelogFile $StagingDir
[System.IO.File]::WriteAllText((Join-Path $StagingDir ".env"), "APIKEY=Replace with apikey`r`n", [System.Text.Encoding]::ASCII)

if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
Compress-Archive -Path (Join-Path $StagingDir "*") -DestinationPath $ZipPath

Remove-Item $StagingDir -Recurse -Force

# Keep a copy of the changelog next to the zips, and record this as the last version built.
Copy-Item $ChangelogFile $DistDir -Force
Set-Content -Path $VersionFile -Value $Version -NoNewline

Write-Host "Created $ZipPath"
