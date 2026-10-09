using OboxSteam.Infrastructure.Observability;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Security.Claims;

namespace OboxSteam.API.Architecture;

public static class ObservabilityExtensions
{
    private const string DEFAULT_SERVICE_NAME = "oboxsteam-api";
    private const string NPGSQL_SOURCE = "Npgsql";

    private static readonly string[] IgnoredPathPrefixes =
    [
        "/swagger",
        "/hubs",
        "/custom-swagger.js",
        "/favicon.ico",
        "/index.html",
    ];

    /// <summary>
    /// Exports traces, metrics and logs to Traceway over OTLP/HTTP. Does nothing unless both
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> (base URL ending in <c>/api/otel</c>) and
    /// <c>TRACEWAY_BACKEND_TOKEN</c> are set, so local runs and tests export nothing.
    /// </summary>
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        var endpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]?.TrimEnd('/');
        var token = builder.Configuration["TRACEWAY_BACKEND_TOKEN"];
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(token))
        {
            return builder;
        }

        var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? DEFAULT_SERVICE_NAME;
        var serviceVersion = builder.Configuration["IMAGE_TAG"] ?? "unknown";

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, serviceVersion: serviceVersion)
                .AddAttributes(
                [
                    new KeyValuePair<string, object>("deployment.environment", builder.Environment.EnvironmentName),
                ]))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.Filter = context => !IsIgnoredPath(context.Request.Path);
                    options.EnrichWithHttpResponse = EnrichWithUser;
                })
                .AddHttpClientInstrumentation()
                .AddAWSInstrumentation()
                .AddSource(NPGSQL_SOURCE, TelemetrySources.BACKGROUND_JOBS_SOURCE, TelemetrySources.AI_SOURCE)
                .AddOtlpExporter(options => ConfigureExporter(options, $"{endpoint}/v1/traces", token)))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(options => ConfigureExporter(options, $"{endpoint}/v1/metrics", token)))
            .WithLogging(
                logging => logging
                    .AddProcessor(new TelemetryLogRedactionProcessor())
                    .AddOtlpExporter(options => ConfigureExporter(options, $"{endpoint}/v1/logs", token)),
                options =>
                {
                    options.IncludeFormattedMessage = true;
                    options.IncludeScopes = true;
                });

        return builder;
    }

    private static void ConfigureExporter(OtlpExporterOptions options, string signalEndpoint, string token)
    {
        // Traceway accepts OTLP over HTTP only; the .NET exporter defaults to gRPC.
        options.Protocol = OtlpExportProtocol.HttpProtobuf;
        options.Endpoint = new Uri(signalEndpoint);
        options.Headers = $"Authorization=Bearer {token}";
    }

    private static bool IsIgnoredPath(PathString path) =>
        path == "/" || IgnoredPathPrefixes.Any(prefix => path.StartsWithSegments(prefix));

    private static void EnrichWithUser(Activity activity, HttpResponse response)
    {
        var user = response.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return;
        }

        activity.SetTag("user.id", user.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        activity.SetTag("user.role", user.FindFirst(ClaimTypes.Role)?.Value);
    }
}
