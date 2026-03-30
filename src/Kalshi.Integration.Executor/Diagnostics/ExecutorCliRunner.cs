using System.Globalization;
using System.Text.Json;

namespace Kalshi.Integration.Executor.Diagnostics;

/// <summary>
/// Handles lightweight operational commands that reuse the production DI graph
/// instead of maintaining a separate standalone CLI application.
/// </summary>
public static class ExecutorCliRunner
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static async Task<bool> TryRunAsync(string[] args, IServiceProvider serviceProvider, TextWriter output, CancellationToken cancellationToken)
    {
        if (args.Length == 0 || !string.Equals(args[0], "dlq", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        using var scope = serviceProvider.CreateScope();
        var replayService = scope.ServiceProvider.GetRequiredService<DeadLetterReplayService>();

        if (args.Length < 2)
        {
            throw new InvalidOperationException("Supported dlq commands: inspect, replay.");
        }

        switch (args[1].ToLowerInvariant())
        {
            case "inspect":
                await HandleInspectAsync(args, replayService, output, cancellationToken);
                return true;

            case "replay":
                await HandleReplayAsync(args, replayService, output, cancellationToken);
                return true;

            default:
                throw new InvalidOperationException("Supported dlq commands: inspect, replay.");
        }
    }

    private static async Task HandleInspectAsync(string[] args, DeadLetterReplayService replayService, TextWriter output, CancellationToken cancellationToken)
    {
        var idValue = GetOptionValue(args, "--id");
        if (!string.IsNullOrWhiteSpace(idValue))
        {
            if (!Guid.TryParse(idValue, out var id))
            {
                throw new InvalidOperationException($"'{idValue}' is not a valid GUID for --id.");
            }

            var record = await replayService.GetByIdAsync(id, cancellationToken)
                ?? throw new InvalidOperationException($"Dead-letter record '{id}' was not found.");

            await output.WriteLineAsync(JsonSerializer.Serialize(record, SerializerOptions));
            return;
        }

        var limit = 20;
        var limitValue = GetOptionValue(args, "--limit");
        if (!string.IsNullOrWhiteSpace(limitValue))
        {
            if (!int.TryParse(limitValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out limit) || limit <= 0)
            {
                throw new InvalidOperationException($"'{limitValue}' is not a valid positive integer for --limit.");
            }
        }

        var records = await replayService.ListRecentAsync(limit, cancellationToken);
        await output.WriteLineAsync(JsonSerializer.Serialize(records, SerializerOptions));
    }

    private static async Task HandleReplayAsync(string[] args, DeadLetterReplayService replayService, TextWriter output, CancellationToken cancellationToken)
    {
        var idValue = GetOptionValue(args, "--id")
            ?? throw new InvalidOperationException("The dlq replay command requires --id <dead-letter-record-id>.");

        if (!Guid.TryParse(idValue, out var id))
        {
            throw new InvalidOperationException($"'{idValue}' is not a valid GUID for --id.");
        }

        await replayService.ReplayAsync(id, cancellationToken);
        await output.WriteLineAsync($"Replayed dead-letter record {id}.");
    }

    private static string? GetOptionValue(string[] args, string optionName)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], optionName, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }
}
