# Kalshi Integration Executor

A separate worker application responsible for consuming RabbitMQ events published by the Kalshi Integration Event Publisher, routing by event type, executing Kalshi-side work, and publishing success/failure result events back to RabbitMQ.

This repository starts as the worker skeleton for that downstream executor service.

See `Kalshi.Integration.Executor.md` for the detailed design and implementation plan.
