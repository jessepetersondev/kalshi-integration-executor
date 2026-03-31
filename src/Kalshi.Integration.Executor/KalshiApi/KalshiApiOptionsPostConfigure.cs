using System.Text;
using Microsoft.Extensions.Options;

namespace Kalshi.Integration.Executor.KalshiApi;

/// <summary>
/// Applies post-configuration defaults for kalshi api options.
/// </summary>


public sealed class KalshiApiOptionsPostConfigure : IPostConfigureOptions<KalshiApiOptions>
{
    private readonly IHostEnvironment _hostEnvironment;

    public KalshiApiOptionsPostConfigure(IHostEnvironment hostEnvironment)
    {
        _hostEnvironment = hostEnvironment;
    }

    public void PostConfigure(string? name, KalshiApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AccessKeyId = ResolveConfiguredValue(options.AccessKeyId, options.AccessKeyIdEnvironmentVariable);
        options.PrivateKeyPath = ResolveConfiguredValue(options.PrivateKeyPath, options.PrivateKeyPathEnvironmentVariable);
        options.PrivateKeyPem = ResolvePrivateKeyPem(options);
    }

    private string ResolvePrivateKeyPem(KalshiApiOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.PrivateKeyPem))
        {
            return NormalizePem(options.PrivateKeyPem);
        }

        var inlinePem = ReadEnvironmentVariable(options.PrivateKeyPemEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(inlinePem))
        {
            return NormalizePem(inlinePem);
        }

        var inlinePemBase64 = ReadEnvironmentVariable(options.PrivateKeyPemBase64EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(inlinePemBase64))
        {
            try
            {
                return NormalizePem(Encoding.UTF8.GetString(Convert.FromBase64String(inlinePemBase64)));
            }
            catch (FormatException)
            {
                return inlinePemBase64;
            }
        }

        if (string.IsNullOrWhiteSpace(options.PrivateKeyPath))
        {
            return string.Empty;
        }

        var fullPath = Path.IsPathRooted(options.PrivateKeyPath)
            ? options.PrivateKeyPath
            : Path.Combine(_hostEnvironment.ContentRootPath, options.PrivateKeyPath);

        if (!File.Exists(fullPath))
        {
            return string.Empty;
        }

        return NormalizePem(File.ReadAllText(fullPath));
    }

    private static string ResolveConfiguredValue(string configuredValue, string environmentVariableName)
    {
        if (!string.IsNullOrWhiteSpace(configuredValue))
        {
            return configuredValue.Trim();
        }

        var environmentValue = ReadEnvironmentVariable(environmentVariableName);
        return string.IsNullOrWhiteSpace(environmentValue)
            ? string.Empty
            : environmentValue.Trim();
    }

    private static string? ReadEnvironmentVariable(string environmentVariableName)
        => string.IsNullOrWhiteSpace(environmentVariableName)
            ? null
            : Environment.GetEnvironmentVariable(environmentVariableName);

    private static string NormalizePem(string pem)
        => pem.ReplaceLineEndings("\n").Trim();
}
