# DLQ replay and diagnostics

The executor now persists dead-letter diagnostics in SQLite and exposes simple CLI tooling for inspection and replay.

## Inspect recent dead-letter records

```bash
cd /home/ai/clawd/projects/kalshi-integration-executor

dotnet run --project src/Kalshi.Integration.Executor/Kalshi.Integration.Executor.csproj -- \
  dlq inspect --limit 20
```

## Inspect one dead-letter record

```bash
dotnet run --project src/Kalshi.Integration.Executor/Kalshi.Integration.Executor.csproj -- \
  dlq inspect --id <dead-letter-record-id>
```

## Replay a selected dead-letter record

```bash
dotnet run --project src/Kalshi.Integration.Executor/Kalshi.Integration.Executor.csproj -- \
  dlq replay --id <dead-letter-record-id>
```

## Recorded diagnostics

Each dead-letter record captures:

- dead-letter record ID
- source event ID
- source category + event name
- resource ID / correlation ID / idempotency key
- retry attempt count
- error type + error message
- original serialized payload
- dead-letter timestamp
- replay count + last replay timestamp

## Safe replay workflow

1. inspect the record by ID
2. confirm the root cause is fixed
3. confirm replay will not violate current risk controls
4. replay the selected record intentionally
5. verify a new result event or execution record is produced

Replays publish the **original inbound event payload** back to the executor exchange, so the standard idempotency and risk-control path still applies.
