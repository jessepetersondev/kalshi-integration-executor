# Executor end-to-end resilience suite

The executor now includes an automated resilience-oriented end-to-end suite covering:

- success-path order execution flow through routing + dispatch + durable persistence
- duplicate delivery suppression across process restarts
- dead-letter creation after retry exhaustion
- dead-letter replay back into the inbound event path
- RabbitMQ automatic/topology recovery configuration verification

## Run the full readiness suite

```bash
cd /home/ai/clawd/projects/kalshi-integration-executor
bash scripts/run-end-to-end-suite.sh
```

## Important test classes

- `ExecutorEndToEndResilienceTests`
- `ExecutionReliabilityPolicyTests`
- `DeadLetterReplayServiceTests`
- `ExecutorCliRunnerTests`
- `RabbitMqConnectionFactoryFactoryTests`

## CI/operator-friendly commands

Build + test:

```bash
dotnet build KalshiIntegrationExecutor.sln -c Release /p:TreatWarningsAsErrors=true
dotnet test KalshiIntegrationExecutor.sln -c Release --no-build --verbosity minimal
```

Focused resilience subset:

```bash
dotnet test tests/Kalshi.Integration.Executor.Tests/Kalshi.Integration.Executor.Tests.csproj \
  -c Release \
  --no-build \
  --filter "FullyQualifiedName~ExecutorEndToEndResilienceTests|FullyQualifiedName~DeadLetterReplayServiceTests|FullyQualifiedName~ExecutorCliRunnerTests|FullyQualifiedName~RabbitMqConnectionFactoryFactoryTests" \
  --verbosity minimal
```

## What this does not prove

This suite proves worker behavior, persistence, replay, and recovery wiring locally.
It does **not** place a real production Kalshi order by itself.
Before a true live cutover, still verify:

- live credentials are injected by your runtime
- risk controls match intended exposure limits
- RabbitMQ connectivity is available from the production network
- the target Kalshi market allowlist/denylist is correct
