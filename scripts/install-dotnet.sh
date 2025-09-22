#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
DOTNET_DIR="${DOTNET_INSTALL_DIR:-${REPO_ROOT}/.dotnet}"
DEFAULT_CHANNEL="9.0"

if [[ -n "${1:-}" ]]; then
    VERSION_ARG=("--version" "$1")
    echo "Installing .NET SDK version $1 into ${DOTNET_DIR}" >&2
else
    VERSION_ARG=("--channel" "${DEFAULT_CHANNEL}")
    echo "Installing latest .NET SDK from channel ${DEFAULT_CHANNEL} into ${DOTNET_DIR}" >&2
fi

tmp_dir="$(mktemp -d)"
trap 'rm -rf "${tmp_dir}"' EXIT

install_script="${tmp_dir}/dotnet-install.sh"

curl -sSL https://dot.net/v1/dotnet-install.sh -o "${install_script}"
chmod +x "${install_script}"

"${install_script}" "${VERSION_ARG[@]}" --install-dir "${DOTNET_DIR}" --no-path

echo "Installation complete." >&2
echo
cat <<EOM
To start using the .NET SDK, add the following to your shell session:

  export DOTNET_ROOT="${DOTNET_DIR}"
  export PATH="\$DOTNET_ROOT:\$PATH"

Then verify with:

  dotnet --info
EOM
