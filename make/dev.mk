.PHONY: bundle bundle-unix bundle-windows rtk-init rtk-init-unix rtk-init-windows restore pack fmt-csharp

bundle: bundle-$(BUNDLE_OS)  ## Install development tools for the current OS

bundle-unix:  ## Install macOS/Linux development tools from Brewfile
	@command -v "$(BREW)" >/dev/null 2>&1 || ( \
		echo "Homebrew is not installed or is not on PATH."; \
		echo "Install it from https://brew.sh/ and run make bundle again."; \
		exit 1 \
	)
	@"$(BREW)" bundle --file="$(PROJECT_ROOT)/Brewfile"
	@sh "$(PROJECT_ROOT)/tools/scripts/configure_rtk_unix.sh"

bundle-windows:  ## Install Windows development tools from PowerShell script
	@$(POWERSHELL) -NoProfile -ExecutionPolicy Bypass -File "$(PROJECT_ROOT)/tools/scripts/install_windows_tools.ps1"
	@$(POWERSHELL) -NoProfile -ExecutionPolicy Bypass -File "$(PROJECT_ROOT)/tools/scripts/configure_rtk_windows.ps1"

rtk-init: rtk-init-$(BUNDLE_OS)  ## Install/configure RTK without enabling telemetry

rtk-init-unix:  ## Configure RTK AI agent integrations on macOS/Linux
	@sh "$(PROJECT_ROOT)/tools/scripts/configure_rtk_unix.sh"

rtk-init-windows:  ## Configure RTK AI agent integrations on Windows
	@$(POWERSHELL) -NoProfile -ExecutionPolicy Bypass -File "$(PROJECT_ROOT)/tools/scripts/configure_rtk_windows.ps1"

restore:  ## Restore NuGet packages for all .NET projects
	@dotnet restore "$(SLNX)"

pack:  ## Build NuGet packages for all publishable projects
	@dotnet pack "$(SLNX)" -c Release -o "$(PROJECT_ROOT)/nupkg" --nologo
	@echo "Packages created in nupkg/. Run 'dotnet tool restore' to install the local tool."

fmt-csharp:  ## Auto-format all first-party C# code
	@dotnet format "$(SLNX)" --verbosity minimal
