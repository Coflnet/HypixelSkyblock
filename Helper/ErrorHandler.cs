using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Coflnet.Sky.Core
{
    public static class ErrorHandler
    {
        static string prefix = "api";
        static Prometheus.Counter errorCount = Prometheus.Metrics.CreateCounter($"sky_{prefix}_error", "Counts the amount of error responses handed out");
        static Prometheus.Counter badRequestCount = Prometheus.Metrics.CreateCounter($"sky_{prefix}_bad_request", "Counts the responses for invalid requests");

        public static void Add(ILogger logger, IApplicationBuilder errorApp, string serviceName)
        {
            prefix = serviceName;
            errorApp.Run(async context =>
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "text/json";

                var exceptionHandlerPathFeature =
                    context.Features.Get<IExceptionHandlerPathFeature>();

                if (exceptionHandlerPathFeature?.Error is CoflnetException ex)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    await context.Response.WriteAsync(
                                    JsonConvert.SerializeObject(new { ex.Slug, ex.Message }));
                    badRequestCount.Inc();
                }
                else
                {
                    using var span = OpenTracing.Util.GlobalTracer.Instance.BuildSpan("error").StartActive();
                    span.Span.Log(exceptionHandlerPathFeature?.Error?.Message);
                    span.Span.Log(exceptionHandlerPathFeature?.Error?.StackTrace);
                    var traceId = System.Net.Dns.GetHostName().Replace(serviceName, "").Trim('-') + "." + span?.Span?.Context?.TraceId;
                    logger.LogError(exceptionHandlerPathFeature?.Error, "fatal request error " + traceId);
                    await context.Response.WriteAsync(
                        JsonConvert.SerializeObject(new ErrorResponse
                        {
                            Slug = "internal_error",
                            Message = $"An unexpected internal error occured. Please check that your request is valid. If it is please report he error and include reference 'traceId'.",
                            Trace = traceId
                        }));
                    errorCount.Inc();
                }
            });
        }
    }
}
