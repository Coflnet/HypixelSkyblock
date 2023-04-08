using System.Collections.Generic;
using System.Diagnostics;
using Coflnet;
using Microsoft.Extensions.Logging;

namespace dev
{
    public class Logger
    {
        public static Logger Instance { get; }
        public static ILoggerFactory Factory { get; }

        static Logger()
        {
            Instance = new Logger();
            Factory = LoggerFactory.Create(b => b.AddConsole());
        }

        public void Log(string message)
        {
            System.Console.WriteLine("Info: " + message);
            LogToActivity(message);
        }

        public void Error(string message)
        {
            System.Console.WriteLine("Error: " + message);
            LogToActivity(message);
            Activity.Current?.SetTag("error", true);
        }

        private static void LogToActivity(string message)
        {
            Activity.Current?.AddEvent(new("log", default, new(new Dictionary<string, object> { { "message", message } })));
        }

        public void Info(string message)
        {
            Log(message);
        }


        public void Error(System.Exception error, string message = null)
        {
            if (message != null)
                Error(message);
            Error(error.ToString());
        }
    }
}