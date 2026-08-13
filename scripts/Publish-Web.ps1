[CmdletBinding()]
param(
    [switch]$AllowDirty,
    [string]$SourceRevision
)

$arguments = @((Join-Path $PSScriptRoot "Publish-Web.mjs"))
if ($AllowDirty)
{
    $arguments += "--allow-dirty"
}

if (-not [string]::IsNullOrWhiteSpace($SourceRevision))
{
    $arguments += @("--source-revision", $SourceRevision)
}

& node @arguments
exit $LASTEXITCODE
