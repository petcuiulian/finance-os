$ErrorActionPreference = 'Stop'
Set-Location "$PSScriptRoot/.."
$folder = 'artifacts/win-x64'
if (!(Test-Path "$folder/FinanceOS.exe")) { throw 'Publish the self-contained Windows folder first.' }
Copy-Item docs/WINDOWS_RELEASE.md "$folder/READ-ME.md"
$files = Get-ChildItem $folder -File -Recurse | Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object FullName
$lines = foreach ($file in $files) {
    $relative = [IO.Path]::GetRelativePath((Resolve-Path $folder), $file.FullName)
    "$( (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant() )  $relative"
}
$lines | Set-Content "$folder/SHA256SUMS.txt" -Encoding utf8
Compress-Archive -Path "$folder/*" -DestinationPath artifacts/finance-os-win-x64.zip -Force
Get-FileHash artifacts/finance-os-win-x64.zip -Algorithm SHA256 | Format-List | Out-File artifacts/package-sha256.txt
