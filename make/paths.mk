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

# Parallel job count for `make acceptance` — override with `make acceptance NPROC=1` to force serial.
NPROC ?= $(shell nproc 2>/dev/null || getconf _NPROCESSORS_ONLN 2>/dev/null || echo 4)

ifeq ($(OS),Windows_NT)
BUNDLE_OS := windows

# GNU Make on Windows resolves the recipe shell via PATH search, which can pick up WSL's
# bash.exe shim (C:\Windows\System32\bash.exe) instead of Git Bash. WSL is not a prerequisite
# for this project, so pin SHELL explicitly to a discovered Git Bash install — the recipes
# throughout make/*.mk are plain POSIX shell that Git Bash (installed with Git for Windows,
# already required to clone this repo) runs natively.
# Override with `rtk make GIT_BASH=<path to bash.exe>` for a non-default Git for Windows install.
_empty :=
_space := $(_empty) $(_empty)

ifeq ($(origin GIT_BASH),undefined)
GIT_BASH := $(strip $(wildcard C:/Program\ Files/Git/bin/bash.exe))
ifeq ($(GIT_BASH),)
GIT_BASH := $(strip $(wildcard C:/Program\ Files\ (x86)/Git/bin/bash.exe))
endif
ifeq ($(GIT_BASH),)
# LOCALAPPDATA can contain spaces (e.g. "C:\Users\John Doe\AppData\Local"); escape them so
# $(wildcard …) treats the path as a single argument rather than a whitespace-delimited list.
_LOCALAPPDATA_ESC := $(subst $(_space),\ ,$(LOCALAPPDATA))
GIT_BASH := $(strip $(wildcard $(_LOCALAPPDATA_ESC)/Programs/Git/bin/bash.exe))
endif
endif

ifeq ($(GIT_BASH),)
$(error Git Bash was not found. These make targets require Git Bash (installed with Git for Windows), not WSL. Install Git for Windows from https://git-scm.com/download/win, or set GIT_BASH=<path to bash.exe> if Git is installed in a non-default location. WSL is not required and is not used by these targets.)
endif

SHELL := $(GIT_BASH)
else
BUNDLE_OS := unix
endif
