$ErrorActionPreference = "Stop"

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $projectDir "CodexQuotaApp.cs"
$output = Join-Path $projectDir "CodexQuota.exe"
$icon = Join-Path $projectDir "CodexQuota.ico"

if (-not (Test-Path -LiteralPath $source)) {
    throw "Source file not found: $source"
}

if (Test-Path -LiteralPath $output) {
    Remove-Item -LiteralPath $output -Force
}

$cscCandidates = @(
    (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"),
    (Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe")
)
$csc = $cscCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

if ($csc) {
    & $csc `
        /nologo `
        /target:winexe `
        /platform:anycpu `
        /out:"$output" `
        /reference:System.Windows.Forms.dll `
        /reference:System.Drawing.dll `
        /reference:System.Management.dll `
        /win32icon:"$icon" `
        "$source"

    if ($LASTEXITCODE -ne 0) {
        throw "csc.exe failed with exit code $LASTEXITCODE"
    }
} else {
    Add-Type `
        -Path $source `
        -ReferencedAssemblies System.Windows.Forms,System.Drawing,System.Management `
        -OutputAssembly $output `
        -OutputType WindowsApplication
}

Write-Host "Built $output"
