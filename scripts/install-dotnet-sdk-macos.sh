#!/usr/bin/env bash
#
# Installs a .NET SDK on macOS from Microsoft's official .pkg, and optionally removes an older one.
#
# Upstream dotnet-install.sh is not an alternative here: it has no .pkg path and no uninstall, and its own
# help scopes it to CI installs "without admin rights" that need not "persist across multiple CI runs". The
# .pkg goes through the system installer, which replaces the shared host without leaving macOS holding a
# stale code signature for it, and records a pkgutil receipt, which is what makes removal clean.
#
# Homebrew's dotnet-sdk cask runs that same .pkg and is the shorter route whenever the newest SDK is what is
# wanted. It cannot serve this script's case. The cask carries only the newest version, so a pin it has
# already moved past is unreachable. It declares no auto_updates, so a plain brew upgrade walks the SDK out
# from under a pin set to rollForward disable. Its uninstall also forgets receipts by a glob covering a whole
# feature line instead of one patch.
#
# See "The SDK Pin" in docs/dev.md.

set -euo pipefail

readonly INSTALL_DIR="/usr/local/share/dotnet"
readonly BUILDS_URL="https://builds.dotnet.microsoft.com/dotnet/Sdk"

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"

INSTALL_VERSION=""
REMOVE_VERSION=""
DRY_RUN="false"
WORKSPACE=""

usage() {
    cat <<EOF
Usage: $(basename "$0") [--install-version=<sdk>] [--remove-version=<sdk>] [--dry-run]

  -iv, --install-version=<sdk>   SDK to install, such as 10.0.403. Defaults to sdk.version in global.json.
  -rv, --remove-version=<sdk>    SDK to delete once the install is verified, such as 10.0.401.
  -n,  --dry-run                 Report each step and change nothing.
  -h,  --help                    Show this message.

A value attaches with = or follows as the next argument: -iv=10.0.403, --install-version 10.0.403.
EOF
}

info() { printf '==> %s\n' "$1"; }
die() { printf 'error: %s\n' "$1" >&2; exit 1; }

# Echoed instead of run under --dry-run, so that a privileged step can be inspected before it happens.
run() {
    if [[ "$DRY_RUN" == "true" ]]; then
        printf '    would run: %s\n' "$*"
    else
        "$@"
    fi
}

# global.json carries one "version" key, under sdk, so the first match is that one.
pinned_version() {
    sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "${REPO_ROOT}/global.json" | head -1
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        -iv=*|--install-version=*) INSTALL_VERSION="${1#*=}"; shift ;;
        -rv=*|--remove-version=*) REMOVE_VERSION="${1#*=}"; shift ;;
        -iv|--install-version) [[ $# -ge 2 ]] || die "$1 requires a value."; INSTALL_VERSION="$2"; shift 2 ;;
        -rv|--remove-version) [[ $# -ge 2 ]] || die "$1 requires a value."; REMOVE_VERSION="$2"; shift 2 ;;
        -n|--dry-run) DRY_RUN="true"; shift ;;
        -h|--help) usage; exit 0 ;;
        *) usage >&2; die "unknown argument '$1'." ;;
    esac
done

trap '[[ -n "$WORKSPACE" ]] && rm -rf "$WORKSPACE"' EXIT

# The .pkg, installer, pkgutil and the receipt identifier are all macOS. Linux needs none of this, since
# nothing there validates the host's signature, so the upstream script is the route.
[[ "$(uname -s)" == "Darwin" ]] \
    || die "this script targets macOS. On Linux use dotnet-install.sh --jsonfile global.json."

[[ "$(uname -m)" == "arm64" ]] && ARCH="arm64" || ARCH="x64"

if [[ -z "$INSTALL_VERSION" ]]; then
    INSTALL_VERSION="$(pinned_version)"
    [[ -n "$INSTALL_VERSION" ]] || die "no --install-version given and global.json names none."
    info "Taking ${INSTALL_VERSION} from global.json."
fi

# rollForward is disable, so removing the pinned SDK would break every dotnet command in the repository.
if [[ -n "$REMOVE_VERSION" ]]; then
    [[ "$REMOVE_VERSION" != "$INSTALL_VERSION" ]] || die "--remove-version names the version being installed."
    [[ "$REMOVE_VERSION" != "$(pinned_version)" ]] || die "--remove-version names the version global.json pins."
fi

if [[ -d "${INSTALL_DIR}/sdk/${INSTALL_VERSION}" ]]; then
    info "SDK ${INSTALL_VERSION} is already installed."
else
    WORKSPACE="$(mktemp -d)"
    PACKAGE="${WORKSPACE}/dotnet-sdk-${INSTALL_VERSION}-osx-${ARCH}.pkg"

    info "Downloading SDK ${INSTALL_VERSION}."
    curl -fSL --progress-bar -o "$PACKAGE" \
        "${BUILDS_URL}/${INSTALL_VERSION}/dotnet-sdk-${INSTALL_VERSION}-osx-${ARCH}.pkg" \
        || die "download failed; check that ${INSTALL_VERSION} exists."

    # The .pkg is signed and notarized by Microsoft, so its own signature is the integrity check and no
    # published hash has to be fetched alongside it.
    pkgutil --check-signature "$PACKAGE" | grep -q "Microsoft Corporation" \
        || die "the package is not signed by Microsoft."

    info "Installing SDK ${INSTALL_VERSION}."
    run sudo installer -pkg "$PACKAGE" -target / || die "the installer failed."
fi

if [[ -n "$REMOVE_VERSION" ]]; then
    if [[ -d "${INSTALL_DIR}/sdk/${REMOVE_VERSION}" ]]; then
        info "Removing SDK ${REMOVE_VERSION}."
        run sudo rm -rf "${INSTALL_DIR}/sdk/${REMOVE_VERSION}"
    fi

    # Deleting the directory alone orphans the receipt, leaving pkgutil claiming the SDK is still present.
    RECEIPT="com.microsoft.dotnet.dev.${REMOVE_VERSION}.component.osx.${ARCH}"

    if pkgutil --pkg-info "$RECEIPT" >/dev/null 2>&1; then
        info "Forgetting ${RECEIPT}."
        run sudo pkgutil --forget "$RECEIPT"
    fi
fi

if [[ "$DRY_RUN" != "true" ]]; then
    SELECTED="$(cd "$REPO_ROOT" && "${INSTALL_DIR}/dotnet" --version 2>/dev/null || echo 'no SDK')"
    info "This repository resolves ${SELECTED}."
fi
