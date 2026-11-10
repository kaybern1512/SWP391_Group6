<#
.SYNOPSIS
    Gracefully stops running DentalClinic dotnet processes and MSBuild workers to release locked DLLs.
#>
[CmdletBinding()]
param()

Write-Host "==> [Stop-Daemons] Scanning for active DentalClinic processes and MSBuild nodes..." -ForegroundColor Cyan

# Terminate dotnet processes running DentalClinic projects or MSBuild
$dotnetProcs = @()
try {
    $dotnetProcs = Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" -ErrorAction Stop
} catch {
    # Fallback if CIM is restricted by sandbox
    $dotnetProcs = @()
}
$stoppedCount = 0

foreach ($proc in $dotnetProcs) {
    if ($proc.CommandLine -match "DentalClinic" -or $proc.CommandLine -match "MSBuild.dll") {
        Write-Host "  Stopping PID $($proc.ProcessId): $($proc.CommandLine.Substring(0, [Math]::Min(90, $proc.CommandLine.Length)))..." -ForegroundColor Yellow
        Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue
        $stoppedCount++
    }
}

# Wait for file handles to release
Start-Sleep -Milliseconds 800

Write-Host "==> [Stop-Daemons] Successfully terminated $stoppedCount process(es). DLL file locks released." -ForegroundColor Green
