[CmdletBinding()]
param(
    [switch]$AllowDirty
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$outputDirectory = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts\web"))
$expectedOutputDirectory = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRoot "artifacts\web"))

if (-not [string]::Equals(
    $outputDirectory,
    $expectedOutputDirectory,
    [System.StringComparison]::OrdinalIgnoreCase))
{
    throw "The web publish output did not resolve to the expected generated directory."
}

$revision = (& git -C $repositoryRoot rev-parse --verify HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $revision -notmatch "^[0-9a-fA-F]{40}$")
{
    throw "The current Git revision could not be determined."
}

& git -C $repositoryRoot diff --quiet --
$workingTreeChanged = $LASTEXITCODE -eq 1
if ($LASTEXITCODE -gt 1)
{
    throw "The working-tree state could not be determined."
}

& git -C $repositoryRoot diff --cached --quiet --
$indexChanged = $LASTEXITCODE -eq 1
if ($LASTEXITCODE -gt 1)
{
    throw "The index state could not be determined."
}

$untrackedBuildInputs = @(& git -C $repositoryRoot ls-files --others --exclude-standard -- `
    src/PbiAssure.Web `
    src/PbiAssure.Core `
    src/PbiAssure.Reporting `
    Directory.Build.props `
    Directory.Build.targets `
    global.json)
if ($LASTEXITCODE -ne 0)
{
    throw "Untracked build inputs could not be determined."
}

$trackedChangesPresent =
    $workingTreeChanged -or $indexChanged -or $untrackedBuildInputs.Count -gt 0
if ($trackedChangesPresent -and -not $AllowDirty)
{
    throw "Tracked changes are present. Commit them before a production publish, or use -AllowDirty for a local review build."
}

$buildRevision = if ($trackedChangesPresent) { "$revision-dirty" } else { $revision }
$assetVersionPlaceholder = "__PBIASSURE_ASSET_VERSION__"
$assetVersionInputs = @(
    (Join-Path $repositoryRoot "src\PbiAssure.Web\wwwroot\index.html"),
    (Join-Path $repositoryRoot "src\PbiAssure.Web\wwwroot\project-picker.js"),
    (Join-Path $repositoryRoot "src\PbiAssure.Web\wwwroot\download.js"),
    (Join-Path $repositoryRoot "src\PbiAssure.Web\wwwroot\report-viewer.html"),
    (Join-Path $repositoryRoot "src\PbiAssure.Web\wwwroot\report-viewer.js")
)
$assetVersionText = ($assetVersionInputs | ForEach-Object {
    [System.IO.File]::ReadAllText($_)
}) -join "`n"
$assetVersionBytes = [System.Text.Encoding]::UTF8.GetBytes($assetVersionText)
$assetVersionHasher = [System.Security.Cryptography.SHA256]::Create()
try
{
    $assetVersionHash = $assetVersionHasher.ComputeHash($assetVersionBytes)
}
finally
{
    $assetVersionHasher.Dispose()
}
$assetVersion = ([System.BitConverter]::ToString($assetVersionHash) -replace "-", "").Substring(0, 12).ToLowerInvariant()

if (Test-Path -LiteralPath $outputDirectory)
{
    Remove-Item -LiteralPath $outputDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path (Split-Path $outputDirectory -Parent) -Force | Out-Null

$publishArguments = @(
    "publish",
    (Join-Path $repositoryRoot "src\PbiAssure.Web"),
    "-c",
    "Release",
    "-o",
    $outputDirectory,
    "-p:SourceRevisionId=$buildRevision"
)
& dotnet @publishArguments

if ($LASTEXITCODE -ne 0)
{
    throw "The web publish failed."
}

$publishedRoot = Join-Path $outputDirectory "wwwroot"
$requiredFiles = @(
    (Join-Path $publishedRoot "index.html"),
    (Join-Path $publishedRoot "_headers"),
    (Join-Path $publishedRoot "report-viewer.html"),
    (Join-Path $publishedRoot "report-viewer.js"),
    (Join-Path $publishedRoot "_framework\blazor.webassembly.js")
)

foreach ($requiredFile in $requiredFiles)
{
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf))
    {
        throw "The clean web publish is incomplete: $requiredFile was not produced."
    }
}

$versionedFiles = @(
    (Join-Path $publishedRoot "index.html"),
    (Join-Path $publishedRoot "download.js"),
    (Join-Path $publishedRoot "report-viewer.html")
)
foreach ($versionedFile in $versionedFiles)
{
    $content = [System.IO.File]::ReadAllText($versionedFile)
    if (-not $content.Contains($assetVersionPlaceholder))
    {
        throw "The web publish could not version browser asset references in $versionedFile."
    }

    [System.IO.File]::WriteAllText(
        $versionedFile,
        $content.Replace($assetVersionPlaceholder, $assetVersion),
        [System.Text.UTF8Encoding]::new($false))
}

Write-Output "Published clean web application: $publishedRoot"
Write-Output "Embedded build revision: $buildRevision"
Write-Output "Browser asset version: $assetVersion"
