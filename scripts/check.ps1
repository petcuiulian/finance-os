$ErrorActionPreference = 'Stop'
Set-Location "$PSScriptRoot/.."
dotnet restore FinanceOS.slnx --locked-mode
if ($LASTEXITCODE) { exit $LASTEXITCODE }
dotnet build FinanceOS.slnx -c Release --no-restore
if ($LASTEXITCODE) { exit $LASTEXITCODE }
dotnet test tests/FinanceOS.Tests -c Release --no-build --no-restore
if ($LASTEXITCODE) { exit $LASTEXITCODE }
dotnet format FinanceOS.slnx --verify-no-changes --no-restore
exit $LASTEXITCODE
