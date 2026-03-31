using System.Text.Json.Serialization;

namespace Kalshi.Integration.Executor.KalshiApi;

/// <summary>
/// Represents a request payload for kalshi create order api.
/// </summary>
public sealed class KalshiCreateOrderApiRequest
{
    [JsonPropertyName("ticker")]
    public required string Ticker { get; init; }

    [JsonPropertyName("client_order_id")]
    public required string ClientOrderId { get; init; }

    [JsonPropertyName("side")]
    public required string Side { get; init; }

    [JsonPropertyName("action")]
    public required string Action { get; init; }

    [JsonPropertyName("count")]
    public int Count { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = "limit";

    [JsonPropertyName("time_in_force")]
    public required string TimeInForce { get; init; }

    [JsonPropertyName("post_only")]
    public bool PostOnly { get; init; }

    [JsonPropertyName("cancel_order_on_pause")]
    public bool CancelOrderOnPause { get; init; }

    [JsonPropertyName("subaccount")]
    public int Subaccount { get; init; }

    [JsonPropertyName("reduce_only")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ReduceOnly { get; init; }

    [JsonPropertyName("yes_price_dollars")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? YesPriceDollars { get; init; }

    [JsonPropertyName("no_price_dollars")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NoPriceDollars { get; init; }
}