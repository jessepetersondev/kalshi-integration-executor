using System.ComponentModel.DataAnnotations;

namespace Kalshi.Integration.Executor.KalshiApi;

public sealed class KalshiApiOptions
{
    public const string SectionName = "Integrations:KalshiApi";

    public bool Enabled { get; set; } = true;

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://api.elections.kalshi.com";

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 10;

    [Range(0, 5)]
    public int RetryAttempts { get; set; } = 2;

    [Required]
    public string ApiKey { get; set; } = "local-dev-key";

    [Required]
    public string ApiSecret { get; set; } = "local-dev-secret";
}
