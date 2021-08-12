using Jaeger.Samplers;
using Jaeger.Senders;
using Jaeger.Senders.Thrift;
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

                Jaeger.Configuration.SenderConfiguration.DefaultSenderResolver = new SenderResolver(loggerFactory)
                        .RegisterSenderFactory<ThriftSenderFactory>();

                var samplingRate = 0.20d;
                var lowerBoundInSeconds = 10d;
                ISampler sampler = new GuaranteedThroughputSampler(samplingRate,lowerBoundInSeconds);
                var config = Jaeger.Configuration.FromEnv(loggerFactory);

                ITracer tracer = config.GetTracerBuilder()
                    .WithSampler(sampler)
                    .Build();

                GlobalTracer.Register(tracer);

                return tracer;
            });
            services.AddOpenTracing();
        }
    }
}
