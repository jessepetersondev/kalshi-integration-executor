using System.Text.Json;


namespace Kalshi.Integration.Executor.KalshiApi;
/// <summary>
/// Parses kalshi order response values.
/// </summary>


public static class KalshiOrderResponseParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static KalshiOrderSnapshot Parse(string rawBody, string fallbackClientOrderId)
    {
        using var document = JsonDocument.Parse(rawBody);
        var root = document.RootElement;
        var order = root.TryGetProperty("order", out var orderElement) ? orderElement : root;

        string? ReadString(string name)
        {
            return order.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }

        var orderId = ReadString("order_id") ?? fallbackClientOrderId;
        var clientOrderId = ReadString("client_order_id") ?? fallbackClientOrderId;
        var ticker = ReadString("ticker");
        var side = ReadString("side");
        var action = ReadString("action");
        var status = ReadString("status");

        return new KalshiOrderSnapshot(orderId, clientOrderId, ticker, side, action, status, rawBody);
    }
}