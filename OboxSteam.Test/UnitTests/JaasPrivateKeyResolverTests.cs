using OboxSteam.Application.Configuration;

namespace OboxSteam.Test.UnitTests;

public sealed class JaasPrivateKeyResolverTests : IDisposable
{
    private readonly string? _savedPath;
    private readonly string? _savedInline;

    public JaasPrivateKeyResolverTests()
    {
        _savedPath = Environment.GetEnvironmentVariable(JaasPrivateKeyResolver.PrivateKeyPathVariable);
        _savedInline = Environment.GetEnvironmentVariable(JaasPrivateKeyResolver.PrivateKeyVariable);
    }

    public void Dispose()
    {
        Restore(JaasPrivateKeyResolver.PrivateKeyPathVariable, _savedPath);
        Restore(JaasPrivateKeyResolver.PrivateKeyVariable, _savedInline);
    }

    [Fact]
    public void Resolve_ReadsFromFile_WhenPathIsSet()
    {
        var pem = "-----BEGIN PRIVATE KEY-----\nTEST\n-----END PRIVATE KEY-----";
        var tempFile = Path.Combine(Path.GetTempPath(), $"jaas-{Guid.NewGuid():N}.pem");
        File.WriteAllText(tempFile, pem);

        try
        {
            Environment.SetEnvironmentVariable(JaasPrivateKeyResolver.PrivateKeyPathVariable, tempFile);
            Environment.SetEnvironmentVariable(JaasPrivateKeyResolver.PrivateKeyVariable, null);

            var result = JaasPrivateKeyResolver.Resolve();

            Assert.Equal(pem, result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Resolve_PrefersPath_OverInline()
    {
        var pemFromFile = "-----BEGIN PRIVATE KEY-----\nFROM-FILE\n-----END PRIVATE KEY-----";
        var tempFile = Path.Combine(Path.GetTempPath(), $"jaas-{Guid.NewGuid():N}.pem");
        File.WriteAllText(tempFile, pemFromFile);

        try
        {
            Environment.SetEnvironmentVariable(JaasPrivateKeyResolver.PrivateKeyPathVariable, tempFile);
            Environment.SetEnvironmentVariable(JaasPrivateKeyResolver.PrivateKeyVariable, "inline-should-not-win");

            var result = JaasPrivateKeyResolver.Resolve();

            Assert.Equal(pemFromFile, result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Resolve_ReadsInline_WhenPathIsUnset()
    {
        Environment.SetEnvironmentVariable(JaasPrivateKeyResolver.PrivateKeyPathVariable, null);
        Environment.SetEnvironmentVariable(JaasPrivateKeyResolver.PrivateKeyVariable, "inline-pem");

        var result = JaasPrivateKeyResolver.Resolve();

        Assert.Equal("inline-pem", result);
    }

    [Fact]
    public void Resolve_Throws_WhenPathPointsToMissingFile()
    {
        Environment.SetEnvironmentVariable(JaasPrivateKeyResolver.PrivateKeyPathVariable, "/nonexistent/jaas-private.pem");
        Environment.SetEnvironmentVariable(JaasPrivateKeyResolver.PrivateKeyVariable, null);

        var ex = Assert.Throws<InvalidOperationException>(JaasPrivateKeyResolver.Resolve);

        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public void Resolve_Throws_WhenNeitherSourceIsConfigured()
    {
        Environment.SetEnvironmentVariable(JaasPrivateKeyResolver.PrivateKeyPathVariable, null);
        Environment.SetEnvironmentVariable(JaasPrivateKeyResolver.PrivateKeyVariable, null);

        var ex = Assert.Throws<InvalidOperationException>(JaasPrivateKeyResolver.Resolve);

        Assert.Contains("JaaS__PrivateKeyPath", ex.Message);
        Assert.Contains("JaaS__PrivateKey", ex.Message);
    }

    private static void Restore(string name, string? value)
    {
        if (value is null)
            Environment.SetEnvironmentVariable(name, null);
        else
            Environment.SetEnvironmentVariable(name, value);
    }
}
