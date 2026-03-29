using System.Text.Json.Serialization;

namespace Kalshi.Integration.Executor.Messaging;

public sealed record ApplicationEventEnvelope(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("resourceId")] string? ResourceId,
    [property: JsonPropertyName("correlationId")] string? CorrelationId,
    [property: JsonPropertyName("idempotencyKey")] string? IdempotencyKey,
    [property: JsonPropertyName("attributes")] IReadOnlyDictionary<string, string?> Attributes,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt);
