using System.Text.Json;
using Kalshi.Integration.Executor.Messaging;
using Kalshi.Integration.Executor.Persistence;

namespace Kalshi.Integration.Executor.Diagnostics;

/// <summary>
/// Coordinates dead letter replay operations.
/// </summary>


public sealed class DeadLetterReplayService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IDeadLetterRecordStore _deadLetterRecordStore;
    private readonly IInboundEventPublisher _inboundEventPublisher;

    public DeadLetterReplayService(IDeadLetterRecordStore deadLetterRecordStore, IInboundEventPublisher inboundEventPublisher)
    {
        _deadLetterRecordStore = deadLetterRecordStore;
        _inboundEventPublisher = inboundEventPublisher;
    }

    public Task<IReadOnlyList<DeadLetterRecord>> ListRecentAsync(int limit = 100, CancellationToken cancellationToken = default)
        => _deadLetterRecordStore.ListRecentAsync(limit, cancellationToken);

    public Task<DeadLetterRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _deadLetterRecordStore.GetByIdAsync(id, cancellationToken);

    public async Task ReplayAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _deadLetterRecordStore.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Dead-letter record '{id}' was not found.");

        var envelope = JsonSerializer.Deserialize<ApplicationEventEnvelope>(record.OriginalPayload, SerializerOptions)
            ?? throw new InvalidOperationException($"Dead-letter record '{id}' does not contain a valid original payload.");

        await _inboundEventPublisher.PublishAsync(envelope, cancellationToken);
        await _deadLetterRecordStore.MarkReplayedAsync(id, DateTimeOffset.UtcNow, cancellationToken);
    }
}
