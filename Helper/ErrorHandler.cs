using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Trace;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace Coflnet.Sky.Core
{
    public class ErrorHandler
    {
        static string prefix = "api";
        static Prometheus.Counter errorCount = Prometheus.Metrics.CreateCounter($"sky_{prefix}_error", "Counts the amount of error responses handed out");
        static Prometheus.Counter badRequestCount = Prometheus.Metrics.CreateCounter($"sky_{prefix}_bad_request", "Counts the responses for invalid requests");
        static JsonSerializerSettings converter = new JsonSerializerSettings() { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver() };
        public static void Add(IApplicationBuilder errorApp, string serviceName)
        {
            var logger = errorApp.ApplicationServices.GetRequiredService<ILogger<ErrorHandler>>();
            Add(logger, errorApp, serviceName);
        }
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
                                    JsonConvert.SerializeObject(new ErrorResponse() { Slug = ex.Slug, Message = ex.Message }, converter));
                    badRequestCount.Inc();
                }
                else
                {
                    var source = context.RequestServices.GetService<ActivitySource>();
                    using var activity = source.StartActivity("error", ActivityKind.Producer);
                    if (activity == null)
                    {
                        logger.LogError("Could not start activity");
                        return;
                    }
                    activity.AddTag("host", System.Net.Dns.GetHostName());
                    activity.AddEvent(new ActivityEvent("error", default, new ActivityTagsCollection(new KeyValuePair<string, object>[] {
                        new ("error", exceptionHandlerPathFeature?.Error?.Message),
                        new ("stack", exceptionHandlerPathFeature?.Error?.StackTrace),
                        new ("path", context.Request.Path),
                        new ("query", context.Request.QueryString) })));
                    var traceId = System.Net.Dns.GetHostName().Replace(serviceName, "").Trim('-') + "." + activity.Context.TraceId;
                    await context.Response.WriteAsync(
                        JsonConvert.SerializeObject(new ErrorResponse
                        {
                            Slug = "internal_error",
                            Message = $"An unexpected internal error occured. Please check that your request is valid. If it is please report he error and include reference '{activity.Context.TraceId}'.",
                            Trace = traceId
                        }, converter));
                    errorCount.Inc();
                }
            });
        }
    }
}
