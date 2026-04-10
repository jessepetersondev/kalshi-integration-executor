using Kalshi.Integration.Executor.KalshiApi;
using Microsoft.Extensions.Hosting;

namespace Kalshi.Integration.Executor.Tests;

public sealed class KalshiApiOptionsPostConfigureTests
{
    [Fact]
    public void PostConfigureShouldResolveSecretsFromEnvironmentVariables()
    {
        const string accessKeyEnv = "EXECUTOR_TEST_ACCESS_KEY";
        const string privateKeyEnv = "EXECUTOR_TEST_PRIVATE_KEY_PEM";
        Environment.SetEnvironmentVariable(accessKeyEnv, "access-key-123");
        Environment.SetEnvironmentVariable(privateKeyEnv, TestPrivateKeyMaterial.RsaPrivateKeyPem);

        try
        {
            var options = new KalshiApiOptions
            {
                AccessKeyIdEnvironmentVariable = accessKeyEnv,
                PrivateKeyPemEnvironmentVariable = privateKeyEnv,
            };

            var postConfigure = new KalshiApiOptionsPostConfigure(new TestHostEnvironment("Production", "/tmp/executor-tests"));
            postConfigure.PostConfigure(name: null, options);

            Assert.Equal("access-key-123", options.AccessKeyId);
            Assert.Equal(TestPrivateKeyMaterial.RsaPrivateKeyPem, options.PrivateKeyPem);
        }
        finally
        {
            Environment.SetEnvironmentVariable(accessKeyEnv, null);
            Environment.SetEnvironmentVariable(privateKeyEnv, null);
        }
    }

    [Fact]
    public void PostConfigureShouldLoadPrivateKeyFromAbsoluteFilePath()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"kalshi-post-configure-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        var keyPath = Path.Combine(tempDirectory, "kalshi-private-key.pem");
        File.WriteAllText(keyPath, TestPrivateKeyMaterial.RsaPrivateKeyPem);

        try
        {
            var options = new KalshiApiOptions
            {
                AccessKeyId = "access-key-123",
                PrivateKeyPath = keyPath,
            };

            var postConfigure = new KalshiApiOptionsPostConfigure(new TestHostEnvironment("Production", tempDirectory));
            postConfigure.PostConfigure(name: null, options);

            Assert.Equal(TestPrivateKeyMaterial.RsaPrivateKeyPem, options.PrivateKeyPem);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName, string contentRootPath)
        {
            EnvironmentName = environmentName;
            ContentRootPath = contentRootPath;
        }

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "Kalshi.Integration.Executor.Tests";

        public string ContentRootPath { get; set; }

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
