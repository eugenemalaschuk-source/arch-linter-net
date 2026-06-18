#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

if ! command -v brew >/dev/null 2>&1; then
  echo "Homebrew is not installed or is not on PATH."
  echo "Install it from https://brew.sh/ and run make bundle again."
  exit 1
fi

echo "Installing tools from Brewfile..."
brew bundle --file="${REPO_ROOT}/Brewfile"

if ! command -v npm >/dev/null 2>&1; then
  echo "npm is not available after brew bundle. Install Node.js and retry."
  exit 1
fi

if ! command -v openspec >/dev/null 2>&1; then
  echo "Installing @fission-ai/openspec via npm..."
  npm install -g @fission-ai/openspec@latest
fi

echo "Unix tool bootstrap complete."
