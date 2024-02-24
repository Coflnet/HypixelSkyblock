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
using System;

namespace Coflnet.Sky.Core
{
    public class ErrorHandler
    {
        public interface IBody
        {
            string Body { get; }
        }
        public class BodyCache : IBody
        {
            public string Body { get; set; }
        }
        static string prefix = "api";
        static Prometheus.Counter errorCount = Prometheus.Metrics.CreateCounter($"sky_{prefix}_error", "Counts the amount of error responses handed out");
        static Prometheus.Counter badRequestCount = Prometheus.Metrics.CreateCounter($"sky_{prefix}_bad_request", "Counts the responses for invalid requests");
        static Prometheus.Counter forwardCount = Prometheus.Metrics.CreateCounter($"sky_{prefix}_error_forward", "Counts the responses for invalid requests");
        static JsonSerializerSettings converter = new JsonSerializerSettings() { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver() };
        public static void Add(IApplicationBuilder errorApp, string serviceName)
        {
            var logger = errorApp.ApplicationServices.GetRequiredService<ILogger<ErrorHandler>>();
            Add(logger, errorApp, serviceName);
        }
        public static void Add(ILogger logger, IApplicationBuilder errorApp, string serviceName)
        {
            errorApp.Use(async (context, next) =>
            {
                // store incomming request body as feature
                if (context.Request.Method != "GET" && context.Request.ContentLength < 300)
                {
                    context.Request.EnableBuffering();
                    var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
                    logger.LogInformation($"Request: {context.Request.Method} {context.Request.Path} {context.Request.QueryString} {body}");
                    context.Request.Body.Position = 0;
                    context.Features.Set<ErrorHandler.IBody>(new ErrorHandler.BodyCache() { Body = body });
                }
                await next.Invoke();
            });
            prefix = serviceName;
            errorApp.Run(async context =>
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "text/json";

                var exceptionHandlerPathFeature =
                    context.Features.Get<IExceptionHandlerPathFeature>();
                var error = exceptionHandlerPathFeature?.Error;
                if (error is CoflnetException ex)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    await context.Response.WriteAsync(
                                    JsonConvert.SerializeObject(new ErrorResponse() { Slug = ex.Slug, Message = ex.Message }, converter));
                    badRequestCount.Inc();
                }
                // CoflnetExceptions are forwarded
                else if (error != null && error.Message.StartsWith("Error calling ") && error.Message.Contains("trace\":"))
                {
                    // Json error response after first :
                    var split = error.Message.Split(":", 2);
                    await context.Response.WriteAsync(split[1]);
                    forwardCount.Inc();
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
                    var body = "not loadable";
                    try
                    {
                        //context.Request.Body.Seek(0, SeekOrigin.Begin);
                        body = context.Features.Get<IBody>()?.Body ?? body;
                        Console.WriteLine("body:\n" + body);
                    }
                    catch (System.Exception e)
                    {
                        logger.LogError(e, "Could not read body");
                    }
                    activity.AddTag("host", Dns.GetHostName());
                    activity.AddEvent(new ActivityEvent("error", default, new ActivityTagsCollection(new KeyValuePair<string, object>[] {
                        new ("error", exceptionHandlerPathFeature?.Error),
                        new ("type", exceptionHandlerPathFeature?.Error?.GetType().Name),
                        new ("path", context.Request.Path),
                        new ("body", body),
                        new ("query", context.Request.QueryString) })));
                    var traceId = Dns.GetHostName().Replace(serviceName, "").Trim('-') + "." + activity.Context.TraceId;
                    await context.Response.WriteAsync(
                        JsonConvert.SerializeObject(new ErrorResponse
                        {
                            Slug = "internal_error",
                            Message = $"An unexpected internal error occured. Please check that your request is valid. If it is please report the error and include reference '{activity.Context.TraceId}'.",
                            Trace = traceId
                        }, converter));
                    errorCount.Inc();
                }
            });
        }
    }
}
