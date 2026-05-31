param(
    [string]$ResultsDirectory = "TestResults",
    [string]$OutputPath = "badges/tests.json"
)

$badge = [ordered]@{
    schemaVersion = 1
    label = "tests"
    message = "unable to parse"
    color = "orange"
}

try {
    $trxFiles = Get-ChildItem -Path $ResultsDirectory -Filter "*.trx" -Recurse -File -ErrorAction Stop

    if (-not $trxFiles) {
        throw "No TRX files were found under '$ResultsDirectory'."
    }

    $total = 0
    $passed = 0
    $failed = 0
    $skipped = 0

    foreach ($trxFile in $trxFiles) {
        [xml]$trx = [System.IO.File]::ReadAllText($trxFile.FullName)
        $counter = $trx.TestRun.ResultSummary.Counters

        if ($null -eq $counter) {
            throw "TRX file '$($trxFile.FullName)' does not contain result counters."
        }

        $total += [int]$counter.total
        $passed += [int]$counter.passed
        $failed += [int]$counter.failed

        if ($counter.notExecuted) {
            $skipped += [int]$counter.notExecuted
        }
    }

    if ($failed -gt 0) {
        $badge.message = "$passed passed, $failed failed"
        $badge.color = "red"
    }
    else {
        $badge.message = "$passed passing"
        $badge.color = "brightgreen"
    }

    $badge.total = $total
    $badge.passed = $passed
    $badge.failed = $failed
    $badge.skipped = $skipped
}
catch {
    Write-Warning $_
}

$outputDirectory = Split-Path -Path $OutputPath -Parent
if ($outputDirectory) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

$json = $badge | ConvertTo-Json -Depth 3
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$resolvedOutputPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
[System.IO.File]::WriteAllText($resolvedOutputPath, $json, $utf8NoBom)
Get-Content -Path $OutputPath
