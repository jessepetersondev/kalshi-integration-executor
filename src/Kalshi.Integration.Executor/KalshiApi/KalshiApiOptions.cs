using System.ComponentModel.DataAnnotations;

namespace Kalshi.Integration.Executor.KalshiApi;

/// <summary>
/// Configures authentication and request behavior for the Kalshi trading API.
/// </summary>
public sealed class KalshiApiOptions
{
    public const string SectionName = "Integrations:KalshiApi";

    public const string DefaultAccessKeyIdEnvironmentVariable = "KALSHI_ACCESS_KEY_ID";

    public const string DefaultPrivateKeyPemEnvironmentVariable = "KALSHI_PRIVATE_KEY_PEM";

    public const string DefaultPrivateKeyPemBase64EnvironmentVariable = "KALSHI_PRIVATE_KEY_PEM_BASE64";

    public const string DefaultPrivateKeyPathEnvironmentVariable = "KALSHI_PRIVATE_KEY_PATH";

    public bool Enabled { get; set; } = true;

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://api.elections.kalshi.com";

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 10;

    [Range(0, 5)]
    public int RetryAttempts { get; set; } = 2;

    public string AccessKeyId { get; set; } = string.Empty;

    public string AccessKeyIdEnvironmentVariable { get; set; } = DefaultAccessKeyIdEnvironmentVariable;

    public string PrivateKeyPath { get; set; } = string.Empty;

    public string PrivateKeyPem { get; set; } = string.Empty;

    public string PrivateKeyPemEnvironmentVariable { get; set; } = DefaultPrivateKeyPemEnvironmentVariable;

    public string PrivateKeyPemBase64EnvironmentVariable { get; set; } = DefaultPrivateKeyPemBase64EnvironmentVariable;

    public string PrivateKeyPathEnvironmentVariable { get; set; } = DefaultPrivateKeyPathEnvironmentVariable;

    public int Subaccount { get; set; }

    [Required]
    public string TimeInForce { get; set; } = "immediate_or_cancel";

    public bool PostOnly { get; set; }

    public bool CancelOrderOnPause { get; set; } = true;
}
