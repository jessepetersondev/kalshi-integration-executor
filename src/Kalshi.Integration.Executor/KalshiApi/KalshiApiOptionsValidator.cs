using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Kalshi.Integration.Executor.KalshiApi;

public sealed class KalshiApiOptionsValidator : IValidateOptions<KalshiApiOptions>
{
    private readonly IHostEnvironment _hostEnvironment;

    public KalshiApiOptionsValidator(IHostEnvironment hostEnvironment)
    {
        _hostEnvironment = hostEnvironment;
    }

    public ValidateOptionsResult Validate(string? name, KalshiApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _))
        {
            failures.Add($"{KalshiApiOptions.SectionName}:BaseUrl must be an absolute URL.");
        }

        if (string.IsNullOrWhiteSpace(options.AccessKeyId))
        {
            failures.Add($"{KalshiApiOptions.SectionName}:AccessKeyId must be configured directly or via {options.AccessKeyIdEnvironmentVariable}.");
        }

        if (string.IsNullOrWhiteSpace(options.PrivateKeyPem))
        {
            failures.Add(
                $"{KalshiApiOptions.SectionName}:PrivateKeyPem must be configured inline, via {options.PrivateKeyPemEnvironmentVariable}/{options.PrivateKeyPemBase64EnvironmentVariable}, or by a readable file path.");
        }

        if (!_hostEnvironment.IsDevelopment() && !string.IsNullOrWhiteSpace(options.PrivateKeyPath) && !Path.IsPathRooted(options.PrivateKeyPath))
        {
            failures.Add($"{KalshiApiOptions.SectionName}:PrivateKeyPath must be absolute outside Development environments.");
        }

        if (!string.IsNullOrWhiteSpace(options.PrivateKeyPem))
        {
            try
            {
                using var rsa = RSA.Create();
                rsa.ImportFromPem(options.PrivateKeyPem);
            }
            catch (Exception exception) when (exception is ArgumentException or CryptographicException)
            {
                failures.Add($"{KalshiApiOptions.SectionName}:PrivateKeyPem is not a valid RSA private key PEM. {exception.Message}");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
