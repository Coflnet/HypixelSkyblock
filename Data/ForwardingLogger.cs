using System;
using Microsoft.Extensions.Logging;

namespace Coflnet.Sky.Kafka;

// Lets Coflnet.Core clients log through the logger the service injected for the SkyCore wrapper.
internal sealed class ForwardingLogger<T>(ILogger inner) : ILogger<T>
{
    public IDisposable BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);
    public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        => inner.Log(logLevel, eventId, state, exception, formatter);
}
