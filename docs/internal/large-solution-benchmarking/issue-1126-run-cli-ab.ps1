[CmdletBinding(DefaultParameterSetName = 'Benchmark')]
param(
    [Parameter(Mandatory = $true, ParameterSetName = 'Benchmark')]
    [string] $BaseCli,

    [Parameter(Mandatory = $true, ParameterSetName = 'Benchmark')]
    [string] $CurrentCli,

    [Parameter(ParameterSetName = 'Benchmark')]
    [ValidateRange(1, 20)]
    [int] $Pairs = 5,

    [Parameter(Mandatory = $true, ParameterSetName = 'MedianTests')]
    [switch] $TestMedian
)

$ErrorActionPreference = 'Continue'

function Get-Median {
    param([double[]] $Values)

    $ordered = @($Values | Sort-Object)
    # PowerShell casts round; floor first so odd sample counts select the center index.
    $middle = [int]([Math]::Floor($ordered.Count / 2))
    if ($ordered.Count % 2 -eq 0) {
        return ($ordered[$middle - 1] + $ordered[$middle]) / 2
    }

    return $ordered[$middle]
}

if ($TestMedian) {
    $cases = @(
        [pscustomobject]@{ Name = 'three'; Values = [double[]]@(10, 20, 30); Expected = 20 },
        [pscustomobject]@{ Name = 'seven'; Values = [double[]]@(10, 20, 30, 40, 50, 60, 70); Expected = 40 },
        [pscustomobject]@{ Name = 'even'; Values = [double[]]@(10, 20, 30, 40); Expected = 25 }
    )

    foreach ($case in $cases) {
        $actual = Get-Median -Values $case.Values
        if ($actual -ne $case.Expected) {
            throw "Median test '$($case.Name)' expected $($case.Expected), got $actual."
        }
    }

    Write-Output 'Median tests passed (3, 7, and 4 values).'
    return
}

$repoRoot = (Get-Location).Path
$fixtureDirectory = Join-Path $repoRoot '.benchmark/issue-1126-phase-share'
$policyPath = Join-Path $fixtureDirectory 'policy.yml'
$baselinePath = Join-Path $fixtureDirectory 'baseline.yml'
$cliPaths = @{
    base = (Resolve-Path -LiteralPath $BaseCli).Path
    current = (Resolve-Path -LiteralPath $CurrentCli).Path
}
$rows = [System.Collections.Generic.List[object]]::new()

for ($pair = 1; $pair -le $Pairs; $pair++) {
    $order = if ($pair % 2 -eq 1) { @('base', 'current') } else { @('current', 'base') }
    foreach ($variant in $order) {
        $output = & dotnet $cliPaths[$variant] --policy $policyPath --mode audit --baseline $baselinePath --timings 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "$variant CLI pair $pair failed with exit code $LASTEXITCODE`n$($output -join "`n")"
        }

        $outputText = $output -join "`n"
        $totalMatch = [regex]::Match($outputText, '(?m)^\s*total\s+(\d+)\s+ms\s*$')
        $phaseMatch = [regex]::Match($outputText, '(?m)^\s*baseline_loading\s+(\d+)\s+ms\s*$')
        if (-not $totalMatch.Success -or -not $phaseMatch.Success) {
            throw "Could not parse timing output for $variant pair $pair`n$outputText"
        }

        $row = [pscustomobject]@{
            Variant = $variant
            Pair = $pair
            TotalMilliseconds = [int]$totalMatch.Groups[1].Value
            BaselineLoadingMilliseconds = [int]$phaseMatch.Groups[1].Value
        }
        $rows.Add($row)
        Write-Output "$variant pair=$pair total=$($row.TotalMilliseconds)ms baseline_loading=$($row.BaselineLoadingMilliseconds)ms"
    }
}

foreach ($variant in @('base', 'current')) {
    $variantRows = @($rows | Where-Object Variant -eq $variant)
    $totalMedian = Get-Median -Values @($variantRows | ForEach-Object TotalMilliseconds)
    $phaseMedian = Get-Median -Values @($variantRows | ForEach-Object BaselineLoadingMilliseconds)
    Write-Output "$variant median total=$($totalMedian)ms baseline_loading=$($phaseMedian)ms"
}

$pairedTotalDeltas = for ($pair = 1; $pair -le $Pairs; $pair++) {
    $baseRow = $rows | Where-Object { $_.Variant -eq 'base' -and $_.Pair -eq $pair }
    $currentRow = $rows | Where-Object { $_.Variant -eq 'current' -and $_.Pair -eq $pair }
    $currentRow.TotalMilliseconds - $baseRow.TotalMilliseconds
}
$pairedPhaseDeltas = for ($pair = 1; $pair -le $Pairs; $pair++) {
    $baseRow = $rows | Where-Object { $_.Variant -eq 'base' -and $_.Pair -eq $pair }
    $currentRow = $rows | Where-Object { $_.Variant -eq 'current' -and $_.Pair -eq $pair }
    $currentRow.BaselineLoadingMilliseconds - $baseRow.BaselineLoadingMilliseconds
}
$totalDeltaMedian = Get-Median -Values @($pairedTotalDeltas)
$phaseDeltaMedian = Get-Median -Values @($pairedPhaseDeltas)
Write-Output "paired median current-minus-base total=${totalDeltaMedian}ms baseline_loading=${phaseDeltaMedian}ms"
