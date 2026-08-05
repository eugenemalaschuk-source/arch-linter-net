.PHONY: bundle bundle-unix bundle-windows restore pack fmt-csharp

bundle: bundle-$(BUNDLE_OS)  ## Install development tools for the current OS

bundle-unix:  ## Install macOS/Linux development tools from Brewfile
	@command -v "$(BREW)" >/dev/null 2>&1 || ( \
		echo "Homebrew is not installed or is not on PATH."; \
		echo "Install it from https://brew.sh/ and run make bundle again."; \
		exit 1 \
	)
	@"$(BREW)" bundle --file="$(PROJECT_ROOT)/Brewfile"

bundle-windows:  ## Install Windows development tools from PowerShell script
	@$(POWERSHELL) -NoProfile -ExecutionPolicy Bypass -File "$(PROJECT_ROOT)/tools/scripts/install_windows_tools.ps1"

restore:  ## Restore NuGet packages for all .NET projects
	@mkdir -p "$(PROJECT_ROOT)/nupkg"
	@for attempt in 1 2 3; do \
		if dotnet restore "$(SLNX)"; then exit 0; fi; \
		if [ "$$attempt" -eq 3 ]; then exit 1; fi; \
		printf 'NuGet restore failed; retrying in %s seconds (attempt %s of 3).\n' "$$((attempt * 10))" "$$attempt"; \
		sleep "$$((attempt * 10))"; \
	done

pack:  ## Build NuGet packages for all publishable projects
	@dotnet pack "$(SLNX)" -c Release -o "$(PROJECT_ROOT)/nupkg" --nologo
	@echo "Packages created in nupkg/. Run 'dotnet tool restore' to install the local tool."

fmt-csharp:  ## Auto-format all first-party C# code
	@dotnet format "$(SLNX)" --verbosity minimal
