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
            message+="\n";
            System.Console.WriteLine(message);
            FileController.AppendLineAs("errors",message);
        }
    }
}