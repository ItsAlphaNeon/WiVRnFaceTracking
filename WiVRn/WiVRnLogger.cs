using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace WiVRn
{
    public static class WiVRnLogger
    {
        private static readonly string LogFilePath;
        private static readonly object FileLock = new object();

        static WiVRnLogger()
        {
            try
            {
                var dllPath = Assembly.GetExecutingAssembly().Location;
                var dir = Path.GetDirectoryName(dllPath) ?? ".";
                LogFilePath = Path.Combine(dir, "WiVRn.log");
            }
            catch
            {
                LogFilePath = "WiVRn.log";
            }
        }

        public static void Log(string message)
        {
            var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n";
            try
            {
                lock (FileLock)
                {
                    File.AppendAllText(LogFilePath, logLine, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                // If logging fails, there's not much we can do. Optionally, swallow or rethrow.
            }
        }
    }
}
