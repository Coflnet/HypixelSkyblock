using Swashbuckle.AspNetCore.SwaggerGen;

namespace Coflnet.Sky.Core;
/// <summary>
/// Exception message transfer ojbect
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Unique slug for the error
    /// </summary>
    public string Slug { get; set; }
    /// <summary>
    /// Human readable message
    /// </summary>
    public string Message { get; set; }
    /// <summary>
    /// Opentelemetry trace id 
    /// </summary>
    public string Trace { get; set; }
}
