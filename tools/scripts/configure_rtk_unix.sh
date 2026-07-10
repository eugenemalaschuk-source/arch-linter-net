#!/usr/bin/env sh
set -eu

RTK_VERSION="v0.42.4"
RTK_COMMIT="5d32d0736f686b69d1e8b9dc45c007d4eb77a0a2"
RTK_INSTALLER_URL="https://raw.githubusercontent.com/rtk-ai/rtk/${RTK_COMMIT}/install.sh"

log() {
  printf '%s\n' "$*"
}

has_command() {
  command -v "$1" >/dev/null 2>&1
}

install_rtk_if_missing() {
  if has_command rtk; then
    log "rtk is already installed: $(rtk --version)"
    return
  fi

  log "rtk is not installed. Installing pinned ${RTK_VERSION}..."
  if has_command brew; then
    brew install rtk
  elif has_command cargo; then
    cargo install --git https://github.com/rtk-ai/rtk --rev "$RTK_COMMIT" --locked
  elif has_command curl; then
    curl -fsSL --proto '=https' --tlsv1.2 "$RTK_INSTALLER_URL" | RTK_VERSION="$RTK_VERSION" sh
    export PATH="$HOME/.local/bin:$PATH"
  else
    log "rtk installation requires Homebrew, Cargo, or curl. Install rtk manually and rerun make rtk-init."
    exit 1
  fi

  if ! has_command rtk; then
    log "rtk was installed but is not on PATH in this shell. Restart the shell or add ~/.local/bin to PATH."
    exit 1
  fi

  log "rtk installed: $(rtk --version)"
}

disable_rtk_telemetry() {
  export RTK_TELEMETRY_DISABLED=1

  if has_command rtk; then
    rtk telemetry disable >/dev/null 2>&1 || true
  fi
}

file_contains() {
  file_path="$1"
  pattern="$2"
  [ -f "$file_path" ] && grep -Eq "$pattern" "$file_path"
}

claude_is_configured() {
  file_contains "$HOME/.claude/settings.json" 'rtk hook claude|rtk-rewrite\.sh'
}

opencode_is_configured() {
  config_home="${XDG_CONFIG_HOME:-$HOME/.config}"
  [ -f "$config_home/opencode/plugins/rtk.ts" ]
}

codex_is_configured() {
  file_contains "$HOME/.codex/AGENTS.md" 'rtk-instructions|RTK\.md|Rust Token Killer'
}

init_claude_if_needed() {
  if claude_is_configured; then
    log "RTK Claude Code integration is already configured."
    return
  fi

  log "Configuring RTK for Claude Code..."
  rtk init --global --auto-patch
}

init_opencode_if_needed() {
  if opencode_is_configured; then
    log "RTK OpenCode integration is already configured."
    return
  fi

  log "Configuring RTK for OpenCode..."
  rtk init --global --opencode
}

init_codex_if_needed() {
  if codex_is_configured; then
    log "RTK Codex integration is already configured."
    return
  fi

  log "Configuring RTK for Codex..."
  rtk init --global --codex
}

install_rtk_if_missing
disable_rtk_telemetry
init_claude_if_needed
disable_rtk_telemetry
init_opencode_if_needed
disable_rtk_telemetry
init_codex_if_needed
disable_rtk_telemetry

log "RTK telemetry status:"
RTK_TELEMETRY_DISABLED=1 rtk telemetry status || true

log "RTK AI agent bootstrap complete. Restart Claude Code, OpenCode, and Codex sessions to apply changes."
