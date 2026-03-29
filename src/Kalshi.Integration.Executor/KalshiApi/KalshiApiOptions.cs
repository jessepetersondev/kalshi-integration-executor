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
    public string AccessKeyId { get; set; } = string.Empty;

    [Required]
    public string PrivateKeyPath { get; set; } = "kalshi.key";

    public string PrivateKeyPem { get; set; } = string.Empty;

    public int Subaccount { get; set; }

    [Required]
    public string TimeInForce { get; set; } = "immediate_or_cancel";

    public bool PostOnly { get; set; }

    public bool CancelOrderOnPause { get; set; } = true;
}
