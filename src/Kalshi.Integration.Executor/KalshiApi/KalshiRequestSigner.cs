using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Kalshi.Integration.Executor.KalshiApi;

/// <summary>
/// Represents kalshi request signer.
/// </summary>


public sealed class KalshiRequestSigner
{
    private readonly KalshiApiOptions _options;

    public KalshiRequestSigner(KalshiApiOptions options)
    {
        _options = options;
    }

    public (string Timestamp, string Signature) Sign(HttpMethod method, string path)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        var message = $"{timestamp}{method.Method.ToUpperInvariant()}{path}";
        var payload = Encoding.UTF8.GetBytes(message);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(_options.PrivateKeyPem);
        var signatureBytes = rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        var signature = Convert.ToBase64String(signatureBytes);
        return (timestamp, signature);
    }
}