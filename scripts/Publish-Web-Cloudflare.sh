#!/usr/bin/env bash
set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "${script_directory}/.." && pwd)"
cd "${repository_root}"

if [[ -z "${CF_PAGES_COMMIT_SHA:-}" ]]; then
    echo "CF_PAGES_COMMIT_SHA is required for a Cloudflare production publish." >&2
    exit 1
fi

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

if ! command -v dotnet >/dev/null 2>&1; then
    sdk_version="$(node -p "JSON.parse(require('node:fs').readFileSync('global.json', 'utf8')).sdk.version")"
    dotnet_directory="${repository_root}/.dotnet"
    install_script="${TMPDIR:-/tmp}/pbiassure-dotnet-install.sh"

    echo "Installing .NET SDK ${sdk_version} required by global.json..."
    curl --fail --silent --show-error --location \
        https://dot.net/v1/dotnet-install.sh \
        --output "${install_script}"
    bash "${install_script}" \
        --version "${sdk_version}" \
        --install-dir "${dotnet_directory}" \
        --no-path
    export PATH="${dotnet_directory}:${PATH}"
fi

dotnet --version
node ./scripts/Publish-Web.mjs --source-revision "${CF_PAGES_COMMIT_SHA}" "$@"
