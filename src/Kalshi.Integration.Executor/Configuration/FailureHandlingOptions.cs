using System.ComponentModel.DataAnnotations;


namespace Kalshi.Integration.Executor.Configuration;

public sealed class FailureHandlingOptions
{
    public const string SectionName = "FailureHandling";

    [Range(0, 10)]
    public int MaxRetryAttempts { get; set; } = 3;

    [Range(0, 30000)]
    public int BaseDelayMilliseconds { get; set; } = 250;
}