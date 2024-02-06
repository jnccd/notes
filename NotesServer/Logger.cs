using System.Diagnostics;
using System.Reflection;

namespace Notes.Server
{
    public class ServerLogger : Notes.Interface.Logger
    {
        private static readonly char _s = Path.DirectorySeparatorChar;
        private readonly string logDir;

        public ServerLogger(string logDir) 
        {
            this.logDir = logDir;
        }

        public override void Write(object o, bool toFile = true)
        {
            var logText = $"{DateTime.Now:u} - {o}";

            Debug.Write(logText);
            Console.Write(logText);

            if (toFile)
                File.AppendAllText(
                    $"{logDir}{_s}client.log",
                    logText
                    );
        }
    }
}
