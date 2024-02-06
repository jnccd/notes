using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Notes.Desktop
{
    public class DesktopLogger : Interface.Logger
    {
        public override void Write(object o, bool toFile = true)
        {
            Debug.Write(o);

            if (toFile)
                File.AppendAllText("client.log", o.ToString());
        }
    }

    public static class Logger
    {
        public static DesktopLogger logger = new();

        public static void Write(object o, bool toFile = true) => logger.Write(o, toFile);
        public static void WriteLine(object o, bool toFile = true) => logger.WriteLine(o, toFile);
    }
}
