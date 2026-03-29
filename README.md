# Kalshi Integration Executor

A separate worker application responsible for consuming RabbitMQ events published by the Kalshi Integration Event Publisher, routing by event type, executing Kalshi-side work, and publishing success/failure result events back to RabbitMQ.

This repository starts as the worker skeleton for that downstream executor service.

See `Kalshi.Integration.Executor.md` for the detailed design and implementation plan.

## Current local RabbitMQ bootstrap

The executor now includes first-pass RabbitMQ topology bootstrap support.

At startup it:
- connects to RabbitMQ using the configured `RabbitMq` section
- ensures the shared exchange exists: `kalshi.integration.events`
- declares executor queues:
  - `kalshi.integration.executor`
  - `kalshi.integration.executor.results`
- declares dead-letter queues:
  - `kalshi.integration.executor.dlq`
  - `kalshi.integration.executor.results.dlq`
- binds queues using:
  - `kalshi.integration.#`
  - `kalshi.integration.results.#`

This is infrastructure bootstrap only for now — message consumption and routing handlers come in later stories.

## Retry and dead-letter behavior

The executor now includes a first-pass reliability policy:
- retries transient failures (`HttpRequestException`, `TaskCanceledException`, `TimeoutException`)
- uses bounded retry attempts from `FailureHandling`
- dead-letters exhausted failures into the configured dead-letter queue

Current configuration section:
- `FailureHandling:MaxRetryAttempts`
- `FailureHandling:BaseDelayMilliseconds`

Current dead-letter path:
- `kalshi.integration.executor.dlq`

## Local end-to-end environment

A local `docker-compose.yml` is now included in this repo for executor-side testing.

It brings up:
- RabbitMQ with management UI
- the executor worker

### Start locally
```bash
cd /home/ai/clawd/projects/kalshi-integration-executor
docker compose up --build
```

### Inspect RabbitMQ
- AMQP: `localhost:5672`
- Management UI: `http://localhost:15672`
- default creds: `guest` / `guest`

### Expected local topology
- exchange: `kalshi.integration.events`
- queues:
  - `kalshi.integration.executor`
  - `kalshi.integration.executor.results`
  - `kalshi.integration.executor.dlq`
  - `kalshi.integration.executor.results.dlq`

### Local workflow with publisher app
1. Start RabbitMQ + executor from this repo.
2. Start the publisher app from `/home/ai/clawd/projects/kalshi-integration-event-publisher` with RabbitMQ enabled.
3. Publish an application event from the publisher app.
4. Verify the exchange and queues in RabbitMQ management UI.
5. Verify executor startup logs show topology bootstrap.
6. For failure-path validation, inspect `kalshi.integration.executor.dlq`.
7. For success-path validation, inspect `kalshi.integration.executor.results`.

### RabbitMQ UI verification for successful runs
Open the RabbitMQ management UI:
- URL: `http://localhost:15673`
- username: `guest`
- password: `guest`

Navigate to **Queues and Streams** and inspect:
- `kalshi.integration.executor` → inbound trading work queue
- `kalshi.integration.executor.results` → successful result events
- `kalshi.integration.executor.dlq` → failed/dead-lettered events

For a successful end-to-end run you should see messages such as:
- `trade-intent.executed`
- `order.execution_succeeded`

The results queue messages are published with routing keys like:
- `kalshi.integration.results.trade_intent_executed`
- `kalshi.integration.results.order_execution_succeeded`

### Validation commands
```bash
cd /home/ai/clawd/projects/kalshi-integration-executor
dotnet build KalshiIntegrationExecutor.sln -c Release /p:TreatWarningsAsErrors=true
dotnet test KalshiIntegrationExecutor.sln -c Release --no-build
docker compose config
```
