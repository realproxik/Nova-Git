param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $projectRoot "artifacts\publish"
$iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue
$isccPath = if ($iscc) { $iscc.Source } else { $null }
if (-not $iscc) {
    $candidate = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    if (Test-Path $candidate) { $isccPath = $candidate }
}
if (-not $isccPath) { throw "Inno Setup 6 is required. Install it from https://jrsoftware.org/isinfo.php and run this script again." }

dotnet publish (Join-Path $projectRoot "NovaGit.csproj") -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $publishDir
& $isccPath "/DMyAppVersion=$Version" (Join-Path $PSScriptRoot "NovaGit.iss")
