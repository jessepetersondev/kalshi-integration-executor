using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Kalshi.Integration.Executor.Logging;


namespace Kalshi.Integration.Executor.KalshiApi;
/// <summary>
/// Provides access to kalshi execution.
/// </summary>


public sealed class KalshiExecutionClient : IKalshiExecutionClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly KalshiApiOptions _options;
    private readonly KalshiRequestSigner _requestSigner;
    private readonly ILogger<KalshiExecutionClient> _logger;

    public KalshiExecutionClient(HttpClient httpClient, IOptions<KalshiApiOptions> options, ILogger<KalshiExecutionClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _requestSigner = new KalshiRequestSigner(_options);
        _logger = logger;
    }

    public async Task<KalshiOrderResponse> PlaceOrderAsync(KalshiOrderRequest request, CancellationToken cancellationToken = default)
    {
        var apiRequest = new KalshiCreateOrderApiRequest
        {
            Ticker = request.MarketTicker,
            ClientOrderId = request.ClientOrderId,
            Side = request.Side,
            Action = request.Action,
            Count = request.Quantity,
            Type = "limit",
            TimeInForce = _options.TimeInForce,
            PostOnly = _options.PostOnly,
            CancelOrderOnPause = _options.CancelOrderOnPause,
            Subaccount = _options.Subaccount,
            ReduceOnly = request.ReduceOnly,
            YesPriceDollars = request.Side.Equals("yes", StringComparison.OrdinalIgnoreCase)
                ? request.LimitPrice.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)
                : null,
            NoPriceDollars = request.Side.Equals("no", StringComparison.OrdinalIgnoreCase)
                ? request.LimitPrice.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)
                : null,
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "/trade-api/v2/portfolio/orders")
        {
            Content = JsonContent.Create(apiRequest, options: SerializerOptions),
        };

        ApplyAuthenticationHeaders(message, "/trade-api/v2/portfolio/orders");
        using var response = await SendAsync(message, "orders.place", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        var snapshot = KalshiOrderResponseParser.Parse(body, request.ClientOrderId);
        return new KalshiOrderResponse(
            snapshot.OrderId,
            snapshot.ClientOrderId,
            snapshot.Ticker,
            snapshot.Side,
            snapshot.Action,
            snapshot.Status ?? response.StatusCode.ToString(),
            snapshot.RawBody);
    }

    public async Task<string> CancelOrderAsync(string externalOrderId, CancellationToken cancellationToken = default)
    {
        var path = $"/trade-api/v2/portfolio/orders/{externalOrderId}";
        using var message = new HttpRequestMessage(HttpMethod.Delete, path);
        ApplyAuthenticationHeaders(message, path);
        using var response = await SendAsync(message, "orders.cancel", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        return body;
    }

    public async Task<string> GetOrderStatusAsync(string externalOrderId, CancellationToken cancellationToken = default)
    {
        var path = $"/trade-api/v2/portfolio/orders/{externalOrderId}";
        using var message = new HttpRequestMessage(HttpMethod.Get, path);
        ApplyAuthenticationHeaders(message, path);
        using var response = await SendAsync(message, "orders.get", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        return body;
    }

    public async Task<string> GetMarketAsync(string marketTicker, CancellationToken cancellationToken = default)
    {
        var path = $"/trade-api/v2/markets/{marketTicker}";
        using var message = new HttpRequestMessage(HttpMethod.Get, path);
        ApplyAuthenticationHeaders(message, path);
        using var response = await SendAsync(message, "markets.get", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        return body;
    }

    private void ApplyAuthenticationHeaders(HttpRequestMessage message, string path)
    {
        var (timestamp, signature) = _requestSigner.Sign(message.Method, path);
        message.Headers.TryAddWithoutValidation("KALSHI-ACCESS-KEY", _options.AccessKeyId);
        message.Headers.TryAddWithoutValidation("KALSHI-ACCESS-SIGNATURE", signature);
        message.Headers.TryAddWithoutValidation("KALSHI-ACCESS-TIMESTAMP", timestamp);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string operation, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            stopwatch.Stop();
            ExecutorLogMessages.KalshiApiCallSucceeded(_logger, operation, (int)response.StatusCode, stopwatch.Elapsed.TotalMilliseconds);
            return response;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            ExecutorLogMessages.KalshiApiCallFailed(_logger, operation, stopwatch.Elapsed.TotalMilliseconds, exception.Message);
            throw;
        }
    }
}