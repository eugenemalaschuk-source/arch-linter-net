Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Test-Command {
    param([Parameter(Mandatory = $true)][string]$Name)
    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function Add-DirectoryToUserPath {
    param([Parameter(Mandatory = $true)][string]$Directory)

    $userPath = [System.Environment]::GetEnvironmentVariable("PATH", "User")
    if ($userPath -notlike "*$Directory*") {
        [System.Environment]::SetEnvironmentVariable("PATH", "$Directory;$userPath", "User")
        Write-Host "Added to user PATH: $Directory"
    }

    if ($env:PATH -notlike "*$Directory*") {
        $env:PATH = "$Directory;$env:PATH"
    }
}

function Install-RtkIfMissing {
    if (Test-Command "rtk") {
        Write-Host "rtk is already installed:"
        rtk --version
        return
    }

    if (Test-Command "cargo") {
        Write-Host "rtk is not installed. Installing via Cargo from GitHub..."
        cargo install --git https://github.com/rtk-ai/rtk
        if ($LASTEXITCODE -ne 0) {
            throw "cargo install rtk failed with exit code $LASTEXITCODE."
        }

        if (Test-Command "rtk") {
            rtk --version
            return
        }
    }

    Write-Host "rtk is not installed. Downloading latest Windows release from GitHub..."
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/rtk-ai/rtk/releases/latest"
    $asset = $release.assets | Where-Object { $_.name -eq "rtk-x86_64-pc-windows-msvc.zip" } | Select-Object -First 1
    if (-not $asset) {
        throw "Could not find rtk-x86_64-pc-windows-msvc.zip in the latest RTK release."
    }

    $installDir = Join-Path $env:USERPROFILE ".local\bin"
    New-Item -ItemType Directory -Force -Path $installDir | Out-Null

    $zipPath = Join-Path $env:TEMP "rtk-windows.zip"
    $extractDir = Join-Path $env:TEMP "rtk-windows"
    Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
    Remove-Item $extractDir -Recurse -Force -ErrorAction SilentlyContinue

    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zipPath
    Expand-Archive -Path $zipPath -DestinationPath $extractDir -Force

    $rtkExe = Get-ChildItem -Path $extractDir -Filter "rtk.exe" -Recurse | Select-Object -First 1
    if (-not $rtkExe) {
        throw "rtk.exe was not found in the downloaded RTK archive."
    }

    Copy-Item -Path $rtkExe.FullName -Destination (Join-Path $installDir "rtk.exe") -Force
    Add-DirectoryToUserPath -Directory $installDir

    Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
    Remove-Item $extractDir -Recurse -Force -ErrorAction SilentlyContinue

    if (-not (Test-Command "rtk")) {
        throw "rtk.exe was installed to $installDir, but rtk is not available on PATH in this shell."
    }

    Write-Host "rtk installed successfully:"
    rtk --version
}

function Disable-RtkTelemetry {
    $env:RTK_TELEMETRY_DISABLED = "1"
    if (Test-Command "rtk") {
        & rtk telemetry disable | Out-Null
    }
}

function Test-FileContains {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Pattern
    )

    if (-not (Test-Path $Path)) {
        return $false
    }

    return $null -ne (Select-String -Path $Path -Pattern $Pattern -Quiet)
}

function Test-ClaudeConfigured {
    $settingsPath = Join-Path $env:USERPROFILE ".claude\settings.json"
    return Test-FileContains -Path $settingsPath -Pattern "rtk hook claude|rtk-rewrite\.sh"
}

function Test-OpenCodeConfigured {
    $pluginPath = Join-Path $env:USERPROFILE ".config\opencode\plugins\rtk.ts"
    return Test-Path $pluginPath
}

function Test-CodexConfigured {
    $agentsPath = Join-Path $env:USERPROFILE ".codex\AGENTS.md"
    return Test-FileContains -Path $agentsPath -Pattern "rtk-instructions|RTK\.md|Rust Token Killer"
}

function Invoke-RtkInit {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    Write-Host "Configuring RTK for $Name..."
    & rtk @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "rtk $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

Install-RtkIfMissing
Disable-RtkTelemetry

if (Test-ClaudeConfigured) {
    Write-Host "RTK Claude Code integration is already configured."
}
else {
    Invoke-RtkInit -Name "Claude Code" -Arguments @("init", "--global", "--auto-patch")
    Disable-RtkTelemetry
}

if (Test-OpenCodeConfigured) {
    Write-Host "RTK OpenCode integration is already configured."
}
else {
    Invoke-RtkInit -Name "OpenCode" -Arguments @("init", "--global", "--opencode")
    Disable-RtkTelemetry
}

if (Test-CodexConfigured) {
    Write-Host "RTK Codex integration is already configured."
}
else {
    Invoke-RtkInit -Name "Codex" -Arguments @("init", "--global", "--codex")
    Disable-RtkTelemetry
}

Write-Host "RTK telemetry status:"
$env:RTK_TELEMETRY_DISABLED = "1"
& rtk telemetry status

Write-Host "RTK AI agent bootstrap complete. Restart Claude Code, OpenCode, and Codex sessions to apply changes."
