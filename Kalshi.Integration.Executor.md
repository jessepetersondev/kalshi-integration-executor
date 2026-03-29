# Kalshi.Integration.Executor

## Purpose

`Kalshi.Integration.Executor` is the missing downstream worker service that subscribes to RabbitMQ events published by the event-publisher app, routes by event type, performs the actual Kalshi API work, and publishes a success or failure event back to RabbitMQ.

This document is the build specification for that app.

---

## Relationship to the current app

Current app role:
- `Kalshi Integration Event Publisher`
- owns HTTP/API boundary
- validates requests
- persists local state
- publishes application events

Executor app role:
- consumes RabbitMQ events
- routes them to handlers
- calls Kalshi API or external systems
- publishes result events
- handles retries/failure policies

The publisher app should remain primarily a **producer/system-of-record boundary**.
The executor app should be the **consumer/worker/orchestrator**.

---

## Core responsibilities

### 1. Subscribe to RabbitMQ
Listen to the exchange used by the publisher app:
- exchange: `kalshi.integration.events`
- exchange type: `topic`

Bind queues for the event categories we care about.

Recommended primary queue:
- queue: `kalshi.integration.executor`

Recommended routing bindings:
- `kalshi.integration.trading.trade_intent_created`
- `kalshi.integration.trading.order_created`
- `kalshi.integration.trading.execution_update_applied`

Also consider wildcard binding if the executor owns all current event handling:
- `kalshi.integration.#`

---

## Consumed event contract

Consume the serialized `ApplicationEventEnvelope` published by the existing app.

Expected fields:
- `eventId`
- `category`
- `name`
- `resourceId`
- `correlationId`
- `idempotencyKey`
- `attributes`
- `occurredAt`

RabbitMQ headers may also include:
- `event-id`
- `category`
- `event-name`
- `occurred-at`
- `resource-id`
- `correlation-id`
- `idempotency-key`
- `attribute:*`

---

## Event routing model

The executor should deserialize the envelope and route by `name` (and optionally `category`).

### Recommended handler mapping

#### `trade-intent.created`
Possible behavior:
- enrich request with Kalshi market metadata
- validate tradability against live Kalshi state
- optionally create a pending execution task
- publish result event:
  - `trade-intent.executed` or `trade-intent.failed`

#### `order.created`
Primary behavior:
- map internal order to Kalshi API order payload
- call Kalshi API order placement endpoint
- persist external order id / response in executor store
- publish result event:
  - `order.execution_succeeded`
  - `order.execution_failed`

#### `execution-update.applied`
Possible behavior:
- reconcile local order state against Kalshi API state
- forward to audit/analytics/notification workflows
- publish result event:
  - `execution-update.reconciled`
  - `execution-update.reconciliation_failed`

---

## Result-event publishing requirements

After handling any inbound event, the executor should publish a result event back to RabbitMQ.

### Success event examples
- `trade-intent.executed`
- `order.execution_succeeded`
- `execution-update.reconciled`

### Failure event examples
- `trade-intent.failed`
- `order.execution_failed`
- `execution-update.reconciliation_failed`

### Required metadata on result events
- original `correlationId`
- original `resourceId`
- original `idempotencyKey` when present
- executor-generated `eventId`
- executor service name/version
- error code + error message on failure events
- retry count / attempt number when relevant

### Result routing key recommendation
Use the same exchange with predictable routing keys, for example:
- `kalshi.integration.executor.order_execution_succeeded`
- `kalshi.integration.executor.order_execution_failed`

Alternative structure:
- `kalshi.integration.results.order_execution_succeeded`
- `kalshi.integration.results.order_execution_failed`

Keep one style and document it clearly.

---

## Kalshi API execution responsibilities

The executor is where real Kalshi calls belong.

### Supported call types
At minimum, the executor should be designed for:
- place order
- cancel order
- get order status
- get market metadata
- reconcile fills/execution status

### Executor boundaries
Executor should:
- map internal event data to Kalshi API DTOs
- call Kalshi API with resilient HTTP client usage
- handle auth/signing required by Kalshi integration
- log request/response metadata safely
- publish result events for success/failure

Executor should not:
- own upstream HTTP request validation from client apps
- become the main UI/dashboard app
- duplicate business invariants already owned by domain/application layers unless needed for safe execution

---

## Failure handling and retries

### Retry policy
Use bounded retries for transient failures:
- HTTP timeouts
- 429 / throttling
- temporary 5xx responses
- RabbitMQ consumer transient failures

Recommended policy:
- 3-5 retries with exponential backoff + jitter

### Non-retryable failures
Do not blindly retry:
- invalid event payload
- unsupported event type
- permanent Kalshi validation failure
- bad credentials/configuration

### Dead-letter strategy
Configure DLQ infrastructure:
- primary queue: `kalshi.integration.executor`
- dead-letter queue: `kalshi.integration.executor.dlq`
- retry queue(s) optional depending on implementation style

DLQ message should preserve:
- original body
- original headers
- failure reason
- last exception type/message
- attempt count
- timestamp moved to DLQ

---

## Idempotency rules

Executor must be idempotent.

Use the incoming event identity and resource identity to avoid duplicate execution.

Recommended idempotency key strategy:
- primary: `eventId`
- secondary: `idempotencyKey` + `resourceId`

The executor should store processed-event records so repeated RabbitMQ deliveries do not duplicate external Kalshi actions.

---

## Storage requirements for executor

The executor should have its own durable store for:
- processed event ids
- outbound Kalshi API request log
- external Kalshi order ids
- execution attempt history
- retry/dead-letter metadata

Recommended minimal tables/entities:
- `ConsumedEvents`
- `ExecutionAttempts`
- `ExternalOrderMappings`
- `ExecutorAuditRecords`

SQLite is acceptable for local dev; SQL Server/Postgres is better for real distributed use.

---

## Suggested project structure

```text
src/
  Kalshi.Integration.Executor/
    Program.cs
    Configuration/
    Messaging/
    Execution/
    KalshiApi/
    Persistence/
    Routing/
    Observability/
```

Recommended internal folders:
- `Messaging/Consumers`
- `Messaging/Publishers`
- `Routing/Handlers`
- `Execution/Services`
- `KalshiApi/Clients`
- `Persistence/Entities`
- `Persistence/Repositories`

---

## Recommended handler design

Use a dispatcher pattern.

Example contracts:
- `IEventHandler`
- `IEventHandler<TEvent>`
- `IEventRouter`
- `IResultEventPublisher`
- `IKalshiExecutionClient`
- `IConsumedEventStore`

Suggested flow:
1. consume RabbitMQ message
2. deserialize envelope
3. check idempotency store
4. dispatch to handler based on event name
5. execute Kalshi API work
6. publish success/failure event
7. ack message only after durable handling outcome is recorded

---

## RabbitMQ topology for local dev

### Exchange
- `kalshi.integration.events` (topic)

### Primary queues
- `kalshi.integration.executor`
- `kalshi.integration.executor.results`

### Dead-letter queues
- `kalshi.integration.executor.dlq`
- `kalshi.integration.executor.results.dlq`

### Bindings
Examples:
- queue `kalshi.integration.executor` binds to `kalshi.integration.#`
- queue `kalshi.integration.executor.results` binds to `kalshi.integration.results.#`

---

## Local environment requirements

Local environment should include RabbitMQ in Docker Compose with management UI enabled.

Recommended container:
- image: `rabbitmq:3-management`

Ports:
- `5672` AMQP
- `15672` management UI

Recommended default local credentials:
- username: `guest`
- password: `guest`

Local testing checklist:
1. start compose
2. verify RabbitMQ reachable
3. verify exchange exists or is auto-declared
4. publish a test order-created event from publisher app
5. confirm executor consumes it
6. confirm executor publishes success/failure result event
7. inspect queues/exchanges in management UI

---

## Observability requirements

The executor should emit:
- structured logs
- correlation id in every log scope
- event id / resource id tagging
- success/failure counters
- retry counters
- handler duration metrics
- Kalshi API dependency timing

Minimum health endpoints / health states:
- RabbitMQ connectivity
- executor store connectivity
- Kalshi API connectivity (or at least config readiness)

---

## Security requirements

- RabbitMQ credentials from configuration/secrets, not hardcoded for non-local environments
- Kalshi API credentials stored securely
- redact sensitive auth values from logs
- validate incoming event shape before execution
- reject unsupported event types explicitly

---

## MVP acceptance criteria for the future executor app

1. consumes events from RabbitMQ locally
2. routes by event name
3. calls a Kalshi API client abstraction
4. publishes success/failure result events back to RabbitMQ
5. supports idempotent re-delivery
6. supports retry + DLQ handling
7. runs locally with Docker Compose and repo documentation
8. includes unit + integration tests for consume/route/publish flow

---

## Recommended next implementation story set

1. create `Kalshi.Integration.Executor` worker project
2. add RabbitMQ consumer abstraction and queue topology bootstrap
3. add event router and typed handlers
4. add Kalshi API client abstraction
5. add result-event publisher
6. add idempotency store + execution history persistence
7. add retry/DLQ behavior
8. add docker-compose wiring and local run docs
