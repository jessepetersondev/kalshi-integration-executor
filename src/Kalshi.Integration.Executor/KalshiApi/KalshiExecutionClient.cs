using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Kalshi.Integration.Executor.Logging;
using Microsoft.Extensions.Options;

namespace Kalshi.Integration.Executor.KalshiApi;

public sealed class KalshiExecutionClient : IKalshiExecutionClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly KalshiApiOptions _options;
    private readonly ILogger<KalshiExecutionClient> _logger;

    public KalshiExecutionClient(HttpClient httpClient, IOptions<KalshiApiOptions> options, ILogger<KalshiExecutionClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<KalshiOrderResponse> PlaceOrderAsync(KalshiOrderRequest request, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/trade-api/v2/orders")
        {
            Content = JsonContent.Create(request, options: SerializerOptions),
        };

        ApplyAuthenticationHeaders(message);
        using var response = await SendAsync(message, "orders.place", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        return new KalshiOrderResponse(request.ClientOrderId, response.StatusCode.ToString(), body);
    }

    public async Task<string> CancelOrderAsync(string externalOrderId, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/trade-api/v2/orders/{externalOrderId}/cancel");
        ApplyAuthenticationHeaders(message);
        using var response = await SendAsync(message, "orders.cancel", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        return body;
    }

    public async Task<string> GetOrderStatusAsync(string externalOrderId, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"/trade-api/v2/orders/{externalOrderId}");
        ApplyAuthenticationHeaders(message);
        using var response = await SendAsync(message, "orders.get", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        return body;
    }

    public async Task<string> GetMarketAsync(string marketTicker, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"/trade-api/v2/markets/{marketTicker}");
        ApplyAuthenticationHeaders(message);
        using var response = await SendAsync(message, "markets.get", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        return body;
    }

    private void ApplyAuthenticationHeaders(HttpRequestMessage message)
    {
        message.Headers.TryAddWithoutValidation("KALSHI-API-KEY", _options.ApiKey);
        message.Headers.TryAddWithoutValidation("KALSHI-API-SECRET", _options.ApiSecret);
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
