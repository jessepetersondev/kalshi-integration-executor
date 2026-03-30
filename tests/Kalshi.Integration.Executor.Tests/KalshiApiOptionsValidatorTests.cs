using Microsoft.Extensions.Hosting;
using Kalshi.Integration.Executor.KalshiApi;
using Microsoft.Extensions.Options;

namespace Kalshi.Integration.Executor.Tests;

public sealed class KalshiApiOptionsValidatorTests
{
    [Fact]
    public void ValidateShouldFailWhenProductionUsesRelativePrivateKeyPath()
    {
        var validator = new KalshiApiOptionsValidator(new TestHostEnvironment("Production"));
        var result = validator.Validate(name: null, new KalshiApiOptions
        {
            BaseUrl = "https://api.elections.kalshi.com",
            AccessKeyId = "access-key-123",
            PrivateKeyPath = "kalshi.key",
            PrivateKeyPem = TestPrivateKeyMaterial.RsaPrivateKeyPem,
        });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("PrivateKeyPath", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateShouldFailWhenPrivateKeyPemIsInvalid()
    {
        var validator = new KalshiApiOptionsValidator(new TestHostEnvironment("Production"));
        var result = validator.Validate(name: null, new KalshiApiOptions
        {
            BaseUrl = "https://api.elections.kalshi.com",
            AccessKeyId = "access-key-123",
            PrivateKeyPem = "not-a-real-private-key",
            PrivateKeyPath = "/run/secrets/kalshi-private-key.pem",
        });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("valid RSA private key PEM", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateShouldSucceedForProductionWithResolvedAbsoluteSecretInputs()
    {
        var validator = new KalshiApiOptionsValidator(new TestHostEnvironment("Production"));
        var result = validator.Validate(name: null, new KalshiApiOptions
        {
            BaseUrl = "https://api.elections.kalshi.com",
            AccessKeyId = "access-key-123",
            PrivateKeyPath = "/run/secrets/kalshi-private-key.pem",
            PrivateKeyPem = TestPrivateKeyMaterial.RsaPrivateKeyPem,
        });

        Assert.Equal(ValidateOptionsResult.Success, result);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "Kalshi.Integration.Executor.Tests";

        public string ContentRootPath { get; set; } = "/tmp/executor-tests";

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
