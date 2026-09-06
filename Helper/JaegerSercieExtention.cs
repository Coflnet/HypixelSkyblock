using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;
using OpenTelemetry;
using System.Diagnostics;
using OpenTelemetry.Resources;
using System.Collections.Concurrent;
using System;
using System.Collections.Generic;

namespace Coflnet.Sky.Core;
public static class JaegerSercieExtention
{
    public static void AddJaeger(this IServiceCollection services, IConfiguration config, double samplingRate = 0.03, double lowerBoundInSeconds = 60)
    {
        var podName = Environment.GetEnvironmentVariable("POD_NAME") ?? Environment.GetEnvironmentVariable("HOSTNAME");
        var resourceAttributes = new Dictionary<string, object>();
        AddIfPresent(resourceAttributes, "service.instance.id", podName);
        AddIfPresent(resourceAttributes, "k8s.pod.name", podName);
        AddIfPresent(resourceAttributes, "k8s.namespace.name", Environment.GetEnvironmentVariable("POD_NAMESPACE"));
        AddIfPresent(resourceAttributes, "k8s.node.name", Environment.GetEnvironmentVariable("NODE_NAME"));

        services.AddOpenTelemetry()
            .WithTracing((builder) => builder
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSqlClientInstrumentation()
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService(config["JAEGER_SERVICE_NAME"] ?? "default")
                .AddAttributes(resourceAttributes))
            .AddOtlpExporter(c=> {
                var v = config["OTEL_EXPORTER_OTLP_TRACES_ENDPOINT"];
                if (string.IsNullOrEmpty(v))
                    throw new ArgumentException("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT is not set");
                c.Endpoint = new Uri(v);
                c.BatchExportProcessorOptions = new BatchExportProcessorOptions<Activity> { 
                    MaxQueueSize = 4096 * 8, MaxExportBatchSize = 1024, ExporterTimeoutMilliseconds = 10000, ScheduledDelayMilliseconds = 800 };
            })
            .SetSampler(new ErrorPreservingSampler(new RationOrTimeBasedSampler(samplingRate, lowerBoundInSeconds)))
        );
    }

    private static void AddIfPresent(Dictionary<string, object> attributes, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            attributes[key] = value;
    }

    /// <summary>Keeps support error spans even when their parent trace was not sampled.</summary>
    public sealed class ErrorPreservingSampler(Sampler rootSampler) : Sampler
    {
        private readonly ParentBasedSampler parentSampler = new(rootSampler);

        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters) =>
            samplingParameters.Name == "error"
                ? new SamplingResult(SamplingDecision.RecordAndSample)
                : parentSampler.ShouldSample(samplingParameters);
    }

    public sealed class RationOrTimeBasedSampler
        : Sampler
    {
        private readonly TraceIdRatioBasedSampler probabilitySampler;
        private readonly TimeSpan minimumSampleInterval;
        private readonly ConcurrentDictionary<string, long> lastSampled = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="RationOrTimeBasedSampler"/> class.
        /// </summary>
        /// <param name="probability">The desired probability of sampling. This must be between 0.0 and 1.0.
        /// Higher the value, higher is the probability of a given Activity to be sampled in.
        /// </param>
        /// <param name="lowerBoundInSeconds">Minimum interval between guaranteed samples of the same root operation.</param>
        public RationOrTimeBasedSampler(double probability, double lowerBoundInSeconds = 30)
        {
            if (probability < 0 || probability > 1)
                throw new ArgumentOutOfRangeException(nameof(probability));
            if (lowerBoundInSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(lowerBoundInSeconds));

            probabilitySampler = new TraceIdRatioBasedSampler(probability);
            minimumSampleInterval = TimeSpan.FromSeconds(lowerBoundInSeconds);
            Description = $"PeriodicOrTraceIdRatioBasedSampler{{{probability}, {minimumSampleInterval}}}";
        }

        /// <inheritdoc />
        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
        {
            if (samplingParameters.Name == "error")
                return new SamplingResult(SamplingDecision.RecordAndSample);
            var now = Stopwatch.GetTimestamp();
            while (true)
            {
                if (!lastSampled.TryGetValue(samplingParameters.Name, out var previous))
                {
                    if (lastSampled.TryAdd(samplingParameters.Name, now))
                        return new SamplingResult(SamplingDecision.RecordAndSample);
                    continue;
                }

                if (Stopwatch.GetElapsedTime(previous, now) < minimumSampleInterval)
                    break;
                if (lastSampled.TryUpdate(samplingParameters.Name, now, previous))
                    return new SamplingResult(SamplingDecision.RecordAndSample);
            }

            return probabilitySampler.ShouldSample(samplingParameters);
        }
    }
}
