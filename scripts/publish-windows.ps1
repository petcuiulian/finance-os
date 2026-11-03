$ErrorActionPreference = 'Stop'
Set-Location "$PSScriptRoot/.."
# Keep the folder together, including SQLite native libraries. No trimming of WPF.
dotnet publish src/FinanceOS.Desktop -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -p:PublishSingleFile=false -p:RestoreLockedMode=true -o artifacts/win-x64
exit $LASTEXITCODE
