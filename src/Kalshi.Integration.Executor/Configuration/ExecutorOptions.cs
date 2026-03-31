using System.ComponentModel.DataAnnotations;

namespace Kalshi.Integration.Executor.Configuration;

/// <summary>
/// Represents configuration for executor.
/// </summary>


public sealed class ExecutorOptions
{
    public const string SectionName = "Executor";

    [Required]
    public string ServiceName { get; set; } = "Kalshi.Integration.Executor";

    [Required]
    public string Mode { get; set; } = "Worker";
}