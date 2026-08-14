[CmdletBinding()]
param(
    [string]$BaseUrl,
    [switch]$SkipBrowserInstall
)

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot "tests\PbiAssure.Privacy.E2E\PbiAssure.Privacy.E2E.csproj"

Push-Location $repositoryRoot
try
{
    & dotnet build $project -c Release
    if ($LASTEXITCODE -ne 0)
    {
        exit $LASTEXITCODE
    }

    if (-not $SkipBrowserInstall)
    {
        $playwright = Join-Path $repositoryRoot "tests\PbiAssure.Privacy.E2E\bin\Release\net10.0\playwright.ps1"
        & $playwright install chromium
        if ($LASTEXITCODE -ne 0)
        {
            exit $LASTEXITCODE
        }
    }

    $previousBaseUrl = $env:PBIASSURE_PRIVACY_BASE_URL
    try
    {
        if ([string]::IsNullOrWhiteSpace($BaseUrl))
        {
            Remove-Item Env:PBIASSURE_PRIVACY_BASE_URL -ErrorAction SilentlyContinue
        }
        else
        {
            $env:PBIASSURE_PRIVACY_BASE_URL = $BaseUrl.TrimEnd('/')
        }

        & dotnet test $project -c Release --no-build
        exit $LASTEXITCODE
    }
    finally
    {
        if ($null -eq $previousBaseUrl)
        {
            Remove-Item Env:PBIASSURE_PRIVACY_BASE_URL -ErrorAction SilentlyContinue
        }
        else
        {
            $env:PBIASSURE_PRIVACY_BASE_URL = $previousBaseUrl
        }
    }
}
finally
{
    Pop-Location
}
