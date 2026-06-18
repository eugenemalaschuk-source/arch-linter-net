# ── Paths ──────────────────────────────────────────────────────────────────
PROJECT_ROOT := $(CURDIR)
TESTS_DIR    := $(PROJECT_ROOT)/tests
RESULTS_DIR  := $(PROJECT_ROOT)/test-results
TOOLS_DIR    := $(PROJECT_ROOT)/tools

SLNX         := $(PROJECT_ROOT)/ArchLinterNet.slnx
CS_SIZE_LINT_ROOTS      ?= src tests docs
CS_SIZE_LINT_WARN_LINES  ?= 500
CS_SIZE_LINT_ERROR_LINES ?= 800

UV        ?= uv
BREW      ?= brew
POWERSHELL ?= pwsh
NPM       ?= npm

ifeq ($(OS),Windows_NT)
BUNDLE_OS := windows
else
BUNDLE_OS := unix
endif
