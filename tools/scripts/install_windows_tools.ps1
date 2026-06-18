Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Test-Command {
    param([Parameter(Mandatory = $true)][string]$Name)
    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function Install-Uv {
    if (Test-Command "uv") {
        Write-Host "uv is already installed."
        uv --version
        return
    }

    Write-Host "uv is not installed. Installing uv..."

    if (Test-Command "winget") {
        Write-Host "Trying winget package: astral-sh.uv"
        winget install --id astral-sh.uv --exact --source winget --accept-package-agreements --accept-source-agreements

        if ($LASTEXITCODE -eq 0 -and (Test-Command "uv")) {
            Write-Host "uv installed successfully via winget."
            uv --version
            return
        }

        Write-Warning "winget did not make uv available on PATH. Falling back to the official Astral installer."
    }
    else {
        Write-Host "winget is not available. Using the official Astral installer."
    }

    Invoke-RestMethod https://astral.sh/uv/install.ps1 | Invoke-Expression

    if (Test-Command "uv") {
        Write-Host "uv installed successfully."
        uv --version
        return
    }

    $userLocalBin = Join-Path $env:USERPROFILE ".local\bin"
    $uvExe = Join-Path $userLocalBin "uv.exe"

    if (Test-Path $uvExe) {
        Write-Host "uv was installed to: $uvExe"
        Write-Host "It is not available on PATH in this shell yet."
        Write-Host "Open a new terminal or add this directory to PATH: $userLocalBin"
        return
    }

    throw "uv installation finished, but uv.exe was not found. Check the installer output above."
}

function Install-NodeJs {
    if (Test-Command "node") {
        $version = node --version
        Write-Host "Node.js $version is already installed."
        return
    }

    Write-Host "Node.js is not installed. Installing Node.js via winget..."

    if (Test-Command "winget") {
        winget install --id OpenJS.NodeJS.LTS --exact --source winget --accept-package-agreements --accept-source-agreements

        if (Test-Command "node") {
            Write-Host "Node.js installed successfully via winget."
            node --version
            return
        }

        Write-Warning "winget Node.js installation did not make node available on PATH. Falling back to official installer."
    }
    else {
        Write-Host "winget is not available. Using the official Node.js installer."
    }

    $installer = "$env:TEMP\node-installer.msi"
    Invoke-WebRequest -Uri "https://nodejs.org/dist/v22.14.0/node-v22.14.0-x64.msi" -OutFile $installer
    Start-Process -Wait -FilePath "msiexec" -ArgumentList "/i `"$installer`" /qn"
    Remove-Item $installer -ErrorAction SilentlyContinue

    $nodePath = "$env:ProgramFiles\nodejs"
    $env:Path = "$nodePath;$env:Path"

    if (Test-Command "node") {
        Write-Host "Node.js installed successfully."
        node --version
        return
    }

    Write-Warning "Node.js was installed but may not be on PATH yet. Restart your terminal."
}

function Install-DotNetSdk {
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
        Write-Host ".NET SDK is already installed."
        dotnet --version
        return
    }

    if (-not (Test-Command "winget")) {
        throw "winget is required to install .NET SDK automatically. Install it or install .NET SDK 10 manually from https://dotnet.microsoft.com/download/dotnet/10.0"
    }

    Write-Host ".NET SDK 10 is not installed or is broken. Installing winget package: Microsoft.DotNet.SDK.10"
    winget install --id Microsoft.DotNet.SDK.10 --exact --source winget --accept-package-agreements --accept-source-agreements

    try {
        $null = & dotnet --version 2>&1
        $installed = $LASTEXITCODE -eq 0
    } catch {
        $installed = $false
    }
    if ($installed) {
        Write-Host ".NET SDK installed successfully."
        dotnet --version
        return
    }

    Write-Warning ".NET SDK may have been installed but dotnet is not functional in this shell. Restart your terminal and try again."
}

function Install-OpenSpec {
    if (Test-Command "openspec") {
        Write-Host "openspec is already installed."
        openspec --version
        return
    }

    Write-Host "openspec is not installed. Installing @fission-ai/openspec via npm..."

    Install-NodeJs

    & "npm" install -g @fission-ai/openspec@latest

    if ($LASTEXITCODE -ne 0) {
        throw "npm install @fission-ai/openspec failed with exit code $LASTEXITCODE."
    }

    if (Test-Command "openspec") {
        Write-Host "openspec installed successfully."
        openspec --version
        return
    }

    $npmGlobalBin = & "npm" config get prefix
    Write-Warning "openspec was installed but is not on PATH in this shell."
    Write-Warning "Add this directory to PATH: $npmGlobalBin"
}

Install-Uv
Install-NodeJs
Install-DotNetSdk
Install-OpenSpec
