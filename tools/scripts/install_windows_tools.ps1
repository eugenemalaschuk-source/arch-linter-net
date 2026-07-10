Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$OpenSpecVersion = "1.6.0"

function Test-Command {
    param([Parameter(Mandatory = $true)][string]$Name)
    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function Install-Uv {
    [CmdletBinding(SupportsShouldProcess)]
    param()

    if (Test-Command "uv") {
        Write-Output "uv is already installed."
        uv --version
        return
    }

    if ($PSCmdlet.ShouldProcess("uv", "Install uv package manager")) {
        Write-Output "uv is not installed. Installing uv..."

        if (Test-Command "winget") {
            Write-Output "Trying winget package: astral-sh.uv"
            winget install --id astral-sh.uv --exact --source winget --accept-package-agreements --accept-source-agreements

            if ($LASTEXITCODE -eq 0 -and (Test-Command "uv")) {
                Write-Output "uv installed successfully via winget."
                uv --version
                return
            }

            Write-Warning "winget did not make uv available on PATH. Falling back to the official Astral installer."
        }
        else {
            Write-Output "winget is not available. Using the official Astral installer."
        }

        Invoke-RestMethod https://astral.sh/uv/install.ps1 | Invoke-Expression

        if (Test-Command "uv") {
            Write-Output "uv installed successfully."
            uv --version
            return
        }

        $userLocalBin = Join-Path $env:USERPROFILE ".local\bin"
        $uvExe = Join-Path $userLocalBin "uv.exe"

        if (Test-Path $uvExe) {
            Write-Output "uv was installed to: $uvExe"
            Write-Output "It is not available on PATH in this shell yet."
            Write-Output "Open a new terminal or add this directory to PATH: $userLocalBin"
            return
        }

        throw "uv installation finished, but uv.exe was not found. Check the installer output above."
    }
}

function Install-NodeJs {
    [CmdletBinding(SupportsShouldProcess)]
    param()

    if (Test-Command "node") {
        $version = node --version
        Write-Output "Node.js $version is already installed."
        return
    }

    if ($PSCmdlet.ShouldProcess("Node.js", "Install Node.js runtime")) {
        Write-Output "Node.js is not installed. Installing Node.js via winget..."

        if (Test-Command "winget") {
            winget install --id OpenJS.NodeJS.LTS --exact --source winget --accept-package-agreements --accept-source-agreements

            if (Test-Command "node") {
                Write-Output "Node.js installed successfully via winget."
                node --version
                return
            }

            Write-Warning "winget Node.js installation did not make node available on PATH. Falling back to official installer."
        }
        else {
            Write-Output "winget is not available. Using the official Node.js installer."
        }

        $installer = "$env:TEMP\node-installer.msi"
        Invoke-WebRequest -Uri "https://nodejs.org/dist/v22.14.0/node-v22.14.0-x64.msi" -OutFile $installer
        Start-Process -Wait -FilePath "msiexec" -ArgumentList "/i `"$installer`" /qn"
        Remove-Item $installer -ErrorAction SilentlyContinue

        $nodePath = "$env:ProgramFiles\nodejs"
        $env:Path = "$nodePath;$env:Path"

        if (Test-Command "node") {
            Write-Output "Node.js installed successfully."
            node --version
            return
        }

        Write-Warning "Node.js was installed but may not be on PATH yet. Restart your terminal."
    }
}

function Install-DotNetSdk {
    [CmdletBinding(SupportsShouldProcess)]
    param()

    $sdkOk = $false
    if (Test-Command "dotnet") {
        try {
            $null = & dotnet --version 2>&1
            $sdkOk = $LASTEXITCODE -eq 0
        } catch {
            $sdkOk = $false
        }
    }

    if ($sdkOk) {
        Write-Output ".NET SDK is already installed."
        dotnet --version
        return
    }

    if ($PSCmdlet.ShouldProcess(".NET SDK 10", "Install .NET SDK")) {
        if (-not (Test-Command "winget")) {
            throw "winget is required to install .NET SDK automatically. Install it or install .NET SDK 10 manually from https://dotnet.microsoft.com/download/dotnet/10.0"
        }

        Write-Output ".NET SDK 10 is not installed or is broken. Installing winget package: Microsoft.DotNet.SDK.10"
        winget install --id Microsoft.DotNet.SDK.10 --exact --source winget --accept-package-agreements --accept-source-agreements

        try {
            $null = & dotnet --version 2>&1
            $installed = $LASTEXITCODE -eq 0
        } catch {
            $installed = $false
        }
        if ($installed) {
            Write-Output ".NET SDK installed successfully."
            dotnet --version
            return
        }

        Write-Warning ".NET SDK may have been installed but dotnet is not functional in this shell. Restart your terminal and try again."
    }
}

function Install-OpenSpec {
    [CmdletBinding(SupportsShouldProcess)]
    param()

    if (Test-Command "openspec") {
        Write-Output "openspec is already installed."
        openspec --version
        return
    }

    if ($PSCmdlet.ShouldProcess("openspec", "Install @fission-ai/openspec@$OpenSpecVersion")) {
        Write-Output "openspec is not installed. Installing @fission-ai/openspec@$OpenSpecVersion via npm..."

        Install-NodeJs

        & "npm" install -g "@fission-ai/openspec@$OpenSpecVersion"

        if ($LASTEXITCODE -ne 0) {
            throw "npm install @fission-ai/openspec@$OpenSpecVersion failed with exit code $LASTEXITCODE."
        }

        if (Test-Command "openspec") {
            Write-Output "openspec installed successfully."
            openspec --version
            return
        }

        $npmGlobalBin = & "npm" config get prefix
        Write-Warning "openspec was installed but is not on PATH in this shell."
        Write-Warning "Add this directory to PATH: $npmGlobalBin"
    }
}

function Install-Actionlint {
    if (Test-Command "actionlint") {
        Write-Output "actionlint is already installed."
        actionlint -version
        return
    }

    Write-Output "actionlint is not installed. Installing latest release binary..."

    $installDir = Join-Path $env:USERPROFILE ".local\bin"
    New-Item -ItemType Directory -Force -Path $installDir | Out-Null

    $scriptPath = Join-Path $env:TEMP "download-actionlint.bash"
    Invoke-WebRequest -Uri "https://raw.githubusercontent.com/rhysd/actionlint/main/scripts/download-actionlint.bash" -OutFile $scriptPath

    if (-not (Test-Command "bash")) {
        throw "bash is required to install actionlint automatically on Windows. Install Git Bash or install actionlint manually."
    }

    & "bash" $scriptPath latest $installDir

    if ($LASTEXITCODE -ne 0) {
        throw "actionlint installation failed with exit code $LASTEXITCODE."
    }

    $env:Path = "$installDir;$env:Path"

    if (Test-Command "actionlint") {
        Write-Output "actionlint installed successfully."
        actionlint -version
        return
    }

    Write-Warning "actionlint was installed but is not on PATH in this shell."
    Write-Warning "Add this directory to PATH: $installDir"
}

function Install-Zizmor {
    if (Test-Command "zizmor") {
        Write-Output "zizmor is already installed."
        zizmor --version
        return
    }

    Write-Output "zizmor is not installed. Installing via cargo-binstall or cargo..."

    if (-not (Test-Command "cargo")) {
        if (-not (Test-Command "winget")) {
            throw "winget is required to install Rust automatically for zizmor. Install Rust manually or install zizmor manually."
        }

        Write-Output "Rust is not installed. Installing Rustup via winget..."
        winget install --id Rustlang.Rustup --exact --source winget --accept-package-agreements --accept-source-agreements

        $cargoBin = Join-Path $env:USERPROFILE ".cargo\bin"
        $env:Path = "$cargoBin;$env:Path"
    }

    if (-not (Test-Command "cargo")) {
        throw "cargo is not available after Rust installation. Restart the shell and run the tool bootstrap again."
    }

    if (-not (Test-Command "cargo-binstall")) {
        & "cargo" install cargo-binstall
        if ($LASTEXITCODE -ne 0) {
            throw "cargo install cargo-binstall failed with exit code $LASTEXITCODE."
        }
    }

    & "cargo-binstall" --no-confirm zizmor
    if ($LASTEXITCODE -ne 0) {
        & "cargo" install zizmor
        if ($LASTEXITCODE -ne 0) {
            throw "zizmor installation failed with exit code $LASTEXITCODE."
        }
    }

    $cargoBin = Join-Path $env:USERPROFILE ".cargo\bin"
    $env:Path = "$cargoBin;$env:Path"

    if (Test-Command "zizmor") {
        Write-Output "zizmor installed successfully."
        zizmor --version
        return
    }

    Write-Warning "zizmor was installed but is not on PATH in this shell."
    Write-Warning "Add this directory to PATH: $cargoBin"
}

Install-Uv
Install-NodeJs
Install-DotNetSdk
Install-OpenSpec
Install-Actionlint
Install-Zizmor
