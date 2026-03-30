#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

echo "==> Building release configuration"
dotnet build KalshiIntegrationExecutor.sln -c Release /p:TreatWarningsAsErrors=true

echo "==> Running full test suite"
dotnet test KalshiIntegrationExecutor.sln -c Release --no-build --verbosity minimal

echo "==> Running resilience-focused subset"
dotnet test tests/Kalshi.Integration.Executor.Tests/Kalshi.Integration.Executor.Tests.csproj \
  -c Release \
  --no-build \
  --filter "FullyQualifiedName~ExecutorEndToEndResilienceTests|FullyQualifiedName~DeadLetterReplayServiceTests|FullyQualifiedName~ExecutorCliRunnerTests|FullyQualifiedName~RabbitMqConnectionFactoryFactoryTests" \
  --verbosity minimal

if command -v docker >/dev/null 2>&1; then
  echo "==> Validating docker compose configuration"
  docker compose config >/dev/null

  if command -v python3 >/dev/null 2>&1; then
    echo "==> Running live RabbitMQ executor smoke harness"
    python3 scripts/run-live-smoke.py --skip-build
  else
    echo "==> Skipping live smoke harness (python3 not installed)"
  fi
else
  echo "==> Skipping docker compose validation (docker not installed)"
fi

echo "==> Executor readiness suite passed"
