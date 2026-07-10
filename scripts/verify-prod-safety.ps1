#Requires -Version 5.1
<#
.SYNOPSIS
  Prod-safety gate for the Composer (#10, ADR 0001). Fails if a Release build of the
  sample host would ship the Plaxtar.Designer assembly or expose the /designer route.

.DESCRIPTION
  Three assertions against a Release publish:
    1. No Plaxtar.Designer.dll (or any designer assembly) in the publish output.
    2. GET /designer returns 404 when run as Production.
    3. GET /_catalog/export (dev endpoint) returns 404 when run as Production.
  The sample host proves the gating pattern any consuming FE inherits: a Debug-only
  ProjectReference plus PLAXTAR_DESIGNER-guarded wiring.

.NOTES
  Run from repo root:  pwsh ./scripts/verify-prod-safety.ps1
  Exit code 0 = safe, 1 = a gate leaked into Release.
#>
[CmdletBinding()]
param(
    [string]$Project = "sample/SampleHost/SampleHost.csproj",
    [int]$Port = 5099
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$out = Join-Path $env:TEMP "plaxtar-prodsafety"
$failures = @()

function Fail($msg) { $script:failures += $msg; Write-Host "  FAIL: $msg" -ForegroundColor Red }
function Pass($msg) { Write-Host "  PASS: $msg" -ForegroundColor Green }

Write-Host "Publishing $Project (Release)..." -ForegroundColor Cyan
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish (Join-Path $repoRoot $Project) -c Release -o $out -v q | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host "publish failed" -ForegroundColor Red; exit 1 }

# 1. Assembly absence
Write-Host "`n[1] Assembly absence"
$leaked = Get-ChildItem $out -Filter "*.dll" | Where-Object { $_.Name -match "Plaxtar|Designer" }
if ($leaked) { Fail "Release output contains: $($leaked.Name -join ', ')" }
else { Pass "no Plaxtar.Designer assembly in Release output" }

# 2 + 3. Route absence (run as Production)
Write-Host "`n[2/3] Route absence (Production)"
$exe = Join-Path $out "SampleHost.exe"
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ASPNETCORE_URLS = "http://localhost:$Port"
$proc = Start-Process -FilePath $exe -WorkingDirectory $out -PassThru -WindowStyle Hidden
try {
    $up = $false
    for ($i = 0; $i -lt 25; $i++) {
        try { if ((Invoke-WebRequest "http://localhost:$Port/" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200) { $up = $true; break } } catch {}
        Start-Sleep -Seconds 1
    }
    if (-not $up) { Fail "host did not start"; }
    else {
        foreach ($route in "/designer", "/_catalog/export") {
            $code = try { (Invoke-WebRequest "http://localhost:$Port$route" -UseBasicParsing -TimeoutSec 3).StatusCode }
                    catch { $_.Exception.Response.StatusCode.value__ }
            if ($code -eq 404) { Pass "$route -> 404" } else { Fail "$route -> $code (expected 404)" }
        }
    }
}
finally {
    if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
}

Write-Host ""
if ($failures.Count -gt 0) { Write-Host "PROD-SAFETY FAILED ($($failures.Count))" -ForegroundColor Red; exit 1 }
Write-Host "PROD-SAFETY OK" -ForegroundColor Green
exit 0
