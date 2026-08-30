namespace OboxSteam.Application.Configuration;

/// <summary>
/// Resolves the JaaS RSA private key PEM from environment configuration.
/// Prefers a mounted file (<c>JaaS__PrivateKeyPath</c>) over inline (<c>JaaS__PrivateKey</c>).
/// </summary>
public static class JaasPrivateKeyResolver
{
    public const string PrivateKeyPathVariable = "JaaS__PrivateKeyPath";
    public const string PrivateKeyVariable = "JaaS__PrivateKey";

    public static string Resolve()
    {
        var path = Environment.GetEnvironmentVariable(PrivateKeyPathVariable);
        if (!string.IsNullOrWhiteSpace(path))
        {
            if (!File.Exists(path))
            {
                throw new InvalidOperationException(
                    $"JaaS private key file not found at '{path}'. Check the volume mount.");
            }

            return File.ReadAllText(path);
        }

        var inline = Environment.GetEnvironmentVariable(PrivateKeyVariable);
        if (!string.IsNullOrWhiteSpace(inline))
            return inline;

        throw new InvalidOperationException(
            "JaaS private key not configured. Set JaaS__PrivateKeyPath (file mount) or JaaS__PrivateKey (inline).");
    }
}
