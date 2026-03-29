using System.Net;
using System.Text;
using System.Text.Json;
using Kalshi.Integration.Executor.KalshiApi;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Kalshi.Integration.Executor.Tests;

public sealed class KalshiExecutionClientTests
{
    [Fact]
    public async Task PlaceOrderAsyncShouldSendExpectedRequest()
    {
        var handler = new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json"),
            });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.elections.kalshi.com"),
        };

        var client = new KalshiExecutionClient(
            httpClient,
            Options.Create(new KalshiApiOptions
            {
                BaseUrl = "https://api.elections.kalshi.com",
                ApiKey = "key-1",
                ApiSecret = "secret-1",
            }),
            NullLogger<KalshiExecutionClient>.Instance);

        var response = await client.PlaceOrderAsync(new KalshiOrderRequest("KXBTC", "yes", 2, 0.42m, "client-123"));

        Assert.Equal("Accepted", response.Status);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://api.elections.kalshi.com/trade-api/v2/orders", handler.LastRequest.RequestUri!.ToString());
        Assert.True(handler.LastRequest.Headers.Contains("KALSHI-API-KEY"));
        Assert.True(handler.LastRequest.Headers.Contains("KALSHI-API-SECRET"));
        using var json = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("KXBTC", json.RootElement.GetProperty("marketTicker").GetString());
        Assert.Equal("client-123", json.RootElement.GetProperty("clientOrderId").GetString());
    }

    [Fact]
    public async Task GetMarketAsyncShouldRequestExpectedEndpoint()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ticker\":\"KXBTC\"}", Encoding.UTF8, "application/json"),
        });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.elections.kalshi.com"),
        };

        var client = new KalshiExecutionClient(
            httpClient,
            Options.Create(new KalshiApiOptions
            {
                BaseUrl = "https://api.elections.kalshi.com",
                ApiKey = "key-1",
                ApiSecret = "secret-1",
            }),
            NullLogger<KalshiExecutionClient>.Instance);

        var result = await client.GetMarketAsync("KXBTC");

        Assert.Contains("KXBTC", result, StringComparison.Ordinal);
        Assert.Equal("https://api.elections.kalshi.com/trade-api/v2/markets/KXBTC", handler.LastRequest!.RequestUri!.ToString());
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastRequestBody { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null
                ? null
                : request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            return Task.FromResult(_responseFactory(request));
        }
    }
}
