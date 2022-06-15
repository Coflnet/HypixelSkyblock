using Jaeger.Samplers;
using Jaeger.Senders;
using Jaeger.Senders.Thrift;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Shims.OpenTracing;
using OpenTelemetry.Trace;
using OpenTracing;
using OpenTracing.Util;

namespace Coflnet.Sky.Core
{
    public static class JaegerSercieExtention
    {
        public static void AddJaeger(this IServiceCollection services, double samplingRate = 0.03, double lowerBoundInSeconds = 30)
        {
            services.AddOpenTelemetryTracing((builder) => builder
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSqlClientInstrumentation()
                .AddJaegerExporter(j=>{
                    j.Protocol = JaegerExportProtocol.HttpBinaryThrift;
                })
                .AddConsoleExporter()
                .SetSampler(new TraceIdRatioBasedSampler(samplingRate))
            );

            services.AddSingleton<ITracer>(serviceProvider =>
            {
                ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                IConfiguration iConfiguration = serviceProvider.GetRequiredService<IConfiguration>();
                /*
                Jaeger.Configuration.SenderConfiguration.DefaultSenderResolver = new SenderResolver(loggerFactory)
                        .RegisterSenderFactory<ThriftSenderFactory>();

                ISampler sampler = new GuaranteedThroughputSampler(samplingRate, lowerBoundInSeconds);
                if (samplingRate >= 1)
                    sampler = new ConstSampler(true);
                var config = Jaeger.Configuration.FromIConfiguration(loggerFactory, iConfiguration);

                ITracer tracer = config.GetTracerBuilder()
                    .WithSampler(sampler)
                    .Build();
                */
                var traceProvider = serviceProvider.GetRequiredService<TracerProvider>();

                // Instantiate the OpenTracing shim. The underlying OpenTelemetry tracer will create
                // spans using the "MyCompany.MyProduct.MyWebServer" source.
                var tracer = new TracerShim(
                    TracerProvider.Default.GetTracer("MyCompany.MyProduct.MyWebServer"),
                    Propagators.DefaultTextMapPropagator);

                try
                {
                    GlobalTracer.Register(tracer);
                }
                catch (System.Exception)
                {
                    loggerFactory.CreateLogger("jager").LogError("Could not register new tracer");
                }


                return tracer;
            });
            //services.AddOpenTracing();
        }
    }
}
