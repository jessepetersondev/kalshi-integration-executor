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
