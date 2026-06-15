using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace Coflnet.Sky.Core;

/// <summary>
/// Shared OpenTelemetry logging configuration for all services in the cluster.
/// Bridges <see cref="ILogger"/> to the OTLP exporter for trace-log correlation.
/// </summary>
public static class OpenTelemetryLoggingExtensions
{
    /// <summary>
    /// Configures the logging pipeline to export logs via OTLP (HttpProtobuf),
    /// with proper resource attributes for service identification and k8s context.
    /// 
    /// In local development (when DEV_LOGGING=true), a simple console logger is used instead.
    /// </summary>
    /// <param name="builder">The <see cref="ILoggingBuilder"/> from <c>Host.CreateDefaultBuilder</c>.</param>
    /// <param name="configuration">Application configuration for reading OTel endpoints and flags.</param>
    /// <param name="applicationName">The service name (used as <c>service.name</c> resource attribute).</param>
    public static void AddOpenTelemetryLogging(this ILoggingBuilder builder, IConfiguration configuration, string applicationName)
    {
        const string devLoggingKey = "DEV_LOGGING";
        const string logLevelPath = "Logging:LogLevel:Default";

        var consoleLogging = configuration.GetValue<bool?>(devLoggingKey) ?? false;

        // Parse configured minimum log level, default to Debug
        var configLogLevel = configuration.GetValue<string>(logLevelPath);
        if (!Enum.TryParse<LogLevel>(configLogLevel, true, out var minLogLevel))
            minLogLevel = LogLevel.Debug;

        // Clear default providers to avoid duplicate output and ensure
        // all logs flow through the OpenTelemetry pipeline.
        builder.ClearProviders();

        builder
            .AddFilter(null, minLogLevel)
            .AddFilter("Microsoft", LogLevel.Warning);

        // In local development, just log to console — no OTLP exporter overhead.
        if (consoleLogging)
        {
            builder.AddSimpleConsole();
            return;
        }

        var resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddService(serviceName: applicationName)
            .AddTelemetrySdk()
            .AddAttributes(GetClusterAttributes(configuration));

        builder.AddOpenTelemetry(logging =>
        {
            logging.SetResourceBuilder(resourceBuilder);
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.ParseStateValues = true;

            logging.AddOtlpExporter(options =>
            {
                options.Protocol = OtlpExportProtocol.HttpProtobuf;
                options.ExportProcessorType = ExportProcessorType.Batch;

                // Logs go to Loki (OTLP-native in Loki 3.x), traces stay on Jaeger.
                // Resolution order: OTEL_EXPORTER_OTLP_LOGS_ENDPOINT → OTEL_EXPORTER_OTLP_ENDPOINT → trace endpoint fallback.
                var logsEndpoint = configuration["OTEL_EXPORTER_OTLP_LOGS_ENDPOINT"]
                                ?? configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
                                ?? configuration["OTEL_EXPORTER_OTLP_TRACES_ENDPOINT"];
                if (!string.IsNullOrEmpty(logsEndpoint))
                    options.Endpoint = new Uri(logsEndpoint);
            });
        });
    }

    /// <summary>
    /// Returns cluster-wide resource attributes — applied to every log record
    /// so logs can be filtered by pod, region, etc. in the observability backend.
    /// </summary>
    private static Dictionary<string, object> GetClusterAttributes(IConfiguration configuration)
    {
        var podName = Environment.GetEnvironmentVariable("OTEL_POD_NAME");
        var region = Environment.GetEnvironmentVariable("LOCATION");
        var result = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(podName))
            result.Add("k8s.pod.name", podName);

        if (!string.IsNullOrEmpty(region))
            result.Add("cloud.region", region);

        return result;
    }
}
