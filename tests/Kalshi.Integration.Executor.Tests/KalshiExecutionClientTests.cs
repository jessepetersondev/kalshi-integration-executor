using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Kalshi.Integration.Executor.KalshiApi;

namespace Kalshi.Integration.Executor.Tests;

public sealed class KalshiExecutionClientTests
{
    private const string TestPrivateKeyPem = "-----BEGIN RSA PRIVATE KEY-----\nMIIEpAIBAAKCAQEAylfnqzDViNsHqfQ9OMIUXC9JPsbO4MBSGbe8a45/6/d+OQNV\neidsV5MWmmOJfUHpsX0gwK37eOdZkQUJ3O5PRt8Jby4X7ilIdXkUlqdKpy0fIG1N\nj4TL7ygU2xuYttkzeC9SiKSHyiZMRq4CvZJkMEdhUzjU7eNsM73ouNfPZ6F/CeMW\nRr1J4Yb9Duq5D05gDOBhg+ggrZDzKoRzHKiQrZkpm68kemLI+7HfsV9KvC1R7mBf\nL+UTpjsFlvL6CsFIDJKn3DrWbATkQPs8axUlv5138vrqAI1VNbAeLF8C36RWVSbF\nEr0vuhLhTI9lYOHe6Pj8gwGnib6TX50b/I/foQIDAQABAoIBAAN8jMYfHwrO5Vyp\nE/X6qCGnge5WPCHUxoVhbFp5F9yvxMnENDCY5c3Df8/0t52EKXvwsUQIq2zbpagx\nS0h2hcCtnqq/A1QL+47koXVwGAH17dOf9oZqzh3GlcdpvBGof/HJ9PTcSuexjb5p\nCKyODXJkhHmL4OVhdg+VLeGjfFQnhRh0pgc97a9L5Ek1mAB3DbhfgoNHBs4VlS6K\nIM2HeTEgNw25gbZBNvBoJZz63I+zVmaGm0+X7FGj3W6F7OB2vBiSNs5hTa3CwMQl\nepSybXWLpjgkABmuW/nF89Ic6/OmOP37wXYnP708WWQ9IfF4Fm9d+yZqbtuX6fr0\nvmEwVv0CgYEAzj+AdmNkTHyma8Dh8/XgFMgjDeQzjTX7gbLkxQo3ozCOWEnJBXH9\nJ43eOxwjxm5fcGEONHcdKHxkFzWZEanHMdrf8KunzCM7xjpKnNIxLBacrbiyFT9Q\nkSKGqtQt3VgLYtUXEB/4wyWcxbRIND2XtGiI8n2vgvcgnaRvCy/ETR0CgYEA+ydG\nzQaqcrdvXo7XPrXItHk5lWoyCQrLKtKhPvQru/0l+xpYDUp/Fx1xQD1kbTkkD31W\nSNPRhDpKTgQYz6MXUTyQ6Ja78i7YEsVJ0g36jZZjoJ6Mjzr75PkNF4Q64Uby3KuW\njHJLZCOLjwV8JD/nSk54MkiEsQHAWELsBSPpSVUCgYBTOhDOtUDuFIbbiJQlbBym\nhjSPEH01CImbRuNGF99nvNpUCkJSLjNn2LnKxIozMqrUoHWo+kAL7FY/f2NrW0WE\nerxPVBV8LOOcFD2zlqY9EkrbV2KVbF1Ik9Qf70sqvLKriS2rVht+NBlVNnDEk+45\n4M0SfWFryemhc49TxkzCiQKBgQCuR1n9CcQbaKjSf+JjNLe6bGiGAzQHTEMhSxnz\nWnJCt+60KVqylmBMhPTCdBeNJ1qbmQjX7oxz6hMHwhYJd43FpHaVv4taCiGMHPUV\n2vdjatllar/04CRNhnkMOYi2LIp4kGUevm0MZxH/w/maGfIAgSUtF19kiOeVF5M6\nGepXJQKBgQDAKVgDx33jBSq+JHR1h5A8nmbOloXyjjB8WzbpDeb5v7cOOaVLi+te\nP1JZKzB3bCzNMdYsuHnOPyyXZlNlO7yC1WVaWyaTMPenqSSeIu7neXzlGysAj6xE\nSbCmLga38rGsCrgcx+YikgdhASUSsICHZTixbDVXfm7lSX8zaVwYxw==\n-----END RSA PRIVATE KEY-----";

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
                AccessKeyId = "key-1",
                PrivateKeyPem = TestPrivateKeyPem,
            }),
            NullLogger<KalshiExecutionClient>.Instance);

        var response = await client.PlaceOrderAsync(new KalshiOrderRequest("KXBTC", "yes", 2, 0.42m, "client-123"));

        Assert.Equal("Accepted", response.Status);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://api.elections.kalshi.com/trade-api/v2/portfolio/orders", handler.LastRequest.RequestUri!.ToString());
        Assert.True(handler.LastRequest.Headers.Contains("KALSHI-ACCESS-KEY"));
        Assert.True(handler.LastRequest.Headers.Contains("KALSHI-ACCESS-SIGNATURE"));
        Assert.True(handler.LastRequest.Headers.Contains("KALSHI-ACCESS-TIMESTAMP"));
        using var json = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("KXBTC", json.RootElement.GetProperty("ticker").GetString());
        Assert.Equal("client-123", json.RootElement.GetProperty("client_order_id").GetString());
        Assert.Equal("buy", json.RootElement.GetProperty("action").GetString());
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
                AccessKeyId = "key-1",
                PrivateKeyPem = TestPrivateKeyPem,
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