#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet restore FinanceOS.slnx --locked-mode
dotnet build FinanceOS.slnx -c Release --no-restore
dotnet test tests/FinanceOS.Tests -c Release --no-build --no-restore
dotnet format FinanceOS.slnx --verify-no-changes --no-restore
