using Coflnet;

namespace dev
{
    public class Logger
    {
        public static Logger Instance {get;}

        static Logger ()
        {
            Instance = new Logger();
        }

        public void Log(string message)
        {
            FileController.AppendLineAs("log",message);
        }

        public void Error(string message)
        {
            System.Console.WriteLine(message);
            try {
                FileController.AppendLineAs("errors",message);
            } catch(System.Exception)
            { }
        }


        public void Error(System.Exception error, string message = null)
        {
            if(message != null)
                Error(message);
            Error($"{error.Message} {error.StackTrace}");
            if(error.InnerException != null)
                Error(error.InnerException);
        }
    }
}