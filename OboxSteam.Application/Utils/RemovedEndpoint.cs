namespace OboxSteam.Application.Utils;

/// <summary>Uniform 410 for routes kept registered after their feature was removed.</summary>
public static class RemovedEndpoint
{
    public const string Code = "ENDPOINT_REMOVED";

    public static Exception Gone(string message = "This endpoint has been removed.")
        => ErrorHelper.Gone(message, Code);
}
