using Jaeger.Samplers;
using Jaeger.Senders;
using Jaeger.Senders.Thrift;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTracing;
using OpenTracing.Util;

namespace hypixel
{
    public static class JaegerSercieExtention
    {
        public static void AddJaeger(this IServiceCollection services)
        {
            services.AddSingleton<ITracer>(serviceProvider =>
            {
                ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                IConfiguration iConfiguration = serviceProvider.GetRequiredService<IConfiguration>();

                Jaeger.Configuration.SenderConfiguration.DefaultSenderResolver = new SenderResolver(loggerFactory)
                        .RegisterSenderFactory<ThriftSenderFactory>();

                var samplingRate = 0.20d;
                var lowerBoundInSeconds = 30d;
                ISampler sampler = new GuaranteedThroughputSampler(samplingRate, lowerBoundInSeconds);
                var config = Jaeger.Configuration.FromIConfiguration(loggerFactory, iConfiguration);

                ITracer tracer = config.GetTracerBuilder()
                    .WithSampler(sampler)
                    .Build();

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
            services.AddOpenTracing();
        }
    }
}
