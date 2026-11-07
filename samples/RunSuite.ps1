# samples/RunSuite.ps1
# Runs the automation against every .html page in this folder and prints one PASS/FAIL row per page.
#   .\RunSuite.ps1            headless (fast)
#   .\RunSuite.ps1 -Visible   shows the browser so you can watch
param([switch]$Visible)

$project = Join-Path $PSScriptRoot '..\src\CloudQATask1.csproj'
$env:HEADLESS = if ($Visible) { '0' } else { '1' }

dotnet build $project -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host 'Build failed.' -ForegroundColor Red; exit 1 }

# frame_*.html are only loaded inside other pages, they are not test pages themselves
$pages = Get-ChildItem -Path $PSScriptRoot -Filter *.html |
    Where-Object { $_.Name -notlike 'frame_*' } |
    Sort-Object Name

$results = foreach ($page in $pages) {
    Write-Host "Running $($page.Name)..." -ForegroundColor Cyan
    $output = dotnet run --project $project -c Release --no-build -- $page.FullName 2>&1
    $code = $LASTEXITCODE
    $notes = ($output | Where-Object { $_ -match '\[FAIL\]|AMBIGUOUS' }) -join ' | '
    [pscustomobject]@{
        Page   = $page.Name
        Result = if ($code -eq 0) { 'PASS' } else { 'FAIL' }
        Notes  = $notes
    }
}

$results | Format-Table -AutoSize -Wrap

$failed = @($results | Where-Object { $_.Result -eq 'FAIL' }).Count
if ($failed -eq 0) { Write-Host "[SUITE PASSED] all $($results.Count) pages" -ForegroundColor Green }
else { Write-Host "[SUITE FAILED] $failed of $($results.Count) pages failed" -ForegroundColor Red }
exit $failed