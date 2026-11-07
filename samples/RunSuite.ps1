# samples/RunSuite.ps1
$samples = @(
    "FlatNoLabels.html",       # Tests attribute/placeholder fallbacks
    "DecoyDuplicates.html",    # Tests Tiered Scoring (Exact Label beating partial placeholder)
    "NestedIframe.html",       # Tests global frame sweeping
    "ShadowDomClone.html",     # Tests the -1 Shadow penalty tie-breaker
    "CustomDropdownDiv.html"   # Tests the ARIA [role='option'] fallback
)

$fails = 0
foreach ($html in $samples) {
    Write-Host "`nRunning against $html..." -ForegroundColor Cyan
    $fullPath = "file:
    dotnet run --project ../src/CloudQATask1.csproj $fullPath
    if ($LASTEXITCODE -ne 0) { $fails++ }
}

if ($fails -eq 0) { Write-Host "`n[SUITE PASSED]" -ForegroundColor Green } 
else { Write-Host "`n[SUITE FAILED] $fails pages failed." -ForegroundColor Red }