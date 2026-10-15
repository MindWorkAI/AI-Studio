#!/usr/bin/env bash
#
# Prepares a cloud session of Claude Code: brings the .NET SDK and the Rust toolchain in line with
# metadata.txt, the way the pipeline reads it, and places the stand-ins which Tauri's build script
# needs before any Rust compiles, like the verify job of the pipeline.
#
# The SessionStart hook in .claude/settings.json runs this script at every start of a session. A cloud
# environment installs its toolchain once, in its setup script, and its sessions then start from a
# snapshot which may be days old while metadata.txt moves on. When everything is in place, this script
# is done within seconds. The setup script of the environment may run it as well, for the first
# installation; it sets CLAUDE_CODE_REMOTE itself, since Claude Code has not started yet.
#
# Usage: prepare-cloud-session.sh [repository root]
# Without an argument, it prepares the repository this script lies in.
#
# Local sessions leave at once: there, the developer's own toolchain and the IDEs build.
#
# What this script prints becomes context of the session. The hook ignores the exit code, so that a
# failure reaches Claude as well; the exit code is meant for the setup script, which must not let a
# broken toolchain into the snapshot of its environment.
#
set -uo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

repo="${1:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
log_file=/tmp/prepare-cloud-session.log
: > "$log_file"

export DOTNET_ROOT="${DOTNET_ROOT:-/usr/share/dotnet}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

say() { echo "[prepare-cloud-session] $*"; }

fail() {
  say "$1 The toolchain is not ready, so builds may fail."
  if [ -s "$log_file" ]; then
    say "The end of $log_file:"
    tail -n 20 "$log_file"
  fi

  exit 1
}

# The same parsing as in .github/workflows/build-and-release.yml:
dotnet_sdk_version=$(sed -n '4p' "$repo/metadata.txt" | sed 's/[^0-9.]*\([0-9.]*\).*/\1/')
rust_version=$(sed -n '6p' "$repo/metadata.txt" | sed 's/[^0-9.]*\([0-9.]*\).*/\1/')
if [ -z "$dotnet_sdk_version" ] || [ -z "$rust_version" ]; then
  fail "Line 4 or line 6 of metadata.txt names no version."
fi

#
# .NET SDK
#
if ! "$DOTNET_ROOT/dotnet" --list-sdks 2>/dev/null | grep -q "^${dotnet_sdk_version} "; then
  say "Installing the .NET SDK $dotnet_sdk_version ..."
  {
    curl -fsSL --retry 3 https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh &&
    bash /tmp/dotnet-install.sh --version "$dotnet_sdk_version" --install-dir "$DOTNET_ROOT" --no-path
  } >> "$log_file" 2>&1 || fail "Installing the .NET SDK $dotnet_sdk_version failed."
fi

ln -sf "$DOTNET_ROOT/dotnet" /usr/local/bin/dotnet

#
# Rust
#
if ! command -v rustup >/dev/null 2>&1 && [ -x "$HOME/.cargo/bin/rustup" ]; then
  export PATH="$HOME/.cargo/bin:$PATH"
fi

if ! command -v rustup >/dev/null 2>&1; then
  say "Installing rustup ..."
  {
    curl --proto '=https' --tlsv1.2 -fsSL https://sh.rustup.rs |
    sh -s -- -y --profile minimal --default-toolchain none --no-modify-path
  } >> "$log_file" 2>&1 || fail "Installing rustup failed."
  export PATH="$HOME/.cargo/bin:$PATH"

  # Ahead of a cargo which the image might keep in /usr/bin:
  for tool in rustup cargo rustc rustdoc cargo-clippy clippy-driver; do
    ln -sf "$HOME/.cargo/bin/$tool" "/usr/local/bin/$tool"
  done
fi

if ! rustup component list --installed --toolchain "$rust_version" 2>/dev/null | grep -q '^clippy'; then
  say "Installing Rust $rust_version with Clippy ..."
  rustup toolchain install "$rust_version" --profile minimal --component clippy >> "$log_file" 2>&1 ||
    fail "Installing Rust $rust_version failed."
fi

rustup default "$rust_version" >> "$log_file" 2>&1 || fail "Rust $rust_version cannot become the default."

#
# Stand-ins, as in the verify job of the pipeline: Tauri's build script refuses to run until the .NET
# sidecar and the downloaded libraries exist, and both are products of a full build. Empty files are
# enough to compile, test, and lint the Rust code. Real files are never replaced.
#
host=$(rustc -vV 2>> "$log_file" | sed -n 's/^host: //p')
if [ -z "$host" ]; then
  fail "rustc does not name its host."
fi

sidecar="$repo/app/MindWork AI Studio/bin/dist/mindworkAIStudioServer-$host"
if [ ! -e "$sidecar" ]; then
  mkdir -p "$(dirname "$sidecar")"
  touch "$sidecar"
  chmod +x "$sidecar"
  say "Placed a stand-in for the .NET sidecar."
fi

libraries="$repo/runtime/resources/libraries"
if [ -z "$(ls -A "$libraries" 2>/dev/null)" ]; then
  mkdir -p "$libraries"
  touch "$libraries/stand-in-for-verify.txt"
  say "Placed a stand-in for the downloaded libraries."
fi

# The .gitignore of the repository covers the sidecar, but not this stand-in. The exclude file of the
# clone keeps it out of every commit without touching the repository:
exclude=$(git -C "$repo" rev-parse --git-path info/exclude 2>/dev/null)
if [ -n "$exclude" ]; then
  case "$exclude" in /*) ;; *) exclude="$repo/$exclude" ;; esac
  if ! grep -qxF '/runtime/resources/libraries/stand-in-for-verify.txt' "$exclude" 2>/dev/null; then
    mkdir -p "$(dirname "$exclude")"
    echo '/runtime/resources/libraries/stand-in-for-verify.txt' >> "$exclude"
  fi
fi

#
# The Linux packages are the setup script's part, since installing them takes minutes: the ICU library
# without which the .NET SDK does not run, and what Tauri compiles against. Tell right away when they
# are missing, rather than let the first build find out:
#
packages=""
if ! ldconfig -p 2>/dev/null | grep -q 'libicuuc\.so'; then
  packages="libicu-dev"
fi

if ! pkg-config --exists webkit2gtk-4.1 2>/dev/null; then
  packages="$packages build-essential pkg-config libwebkit2gtk-4.1-dev libayatana-appindicator3-dev librsvg2-dev"
fi

if [ -n "$packages" ]; then
  say "Builds fail until these Linux packages are installed, which the setup script of the environment normally does: ${packages# }."
fi

say "Toolchain in line with metadata.txt: .NET SDK $dotnet_sdk_version, $(rustc -V). This is a cloud session, so \"Cloud sessions\" in AGENTS.md applies."
exit 0
