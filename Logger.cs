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
    }
}