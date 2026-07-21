using System;
using System.IO;
using MarcasZK.Application;

namespace MarcasZK.Infrastructure
{
    /// <summary>
    /// Dual-output logger: writes to daily log file and Console.
    /// File: {logsDirectory}/yyyyMMdd.txt, appended per line.
    /// </summary>
    public class FileLogger : ILogger
    {
        private readonly string _logsDirectory;

        public FileLogger(string logsDirectory)
        {
            _logsDirectory = logsDirectory;
        }

        public void Log(string message)
        {
            string line = DateTime.Now.ToString("d/M/yyyy HH:mm:ss") + " " + message;
            WriteLine(line);
        }

        public void Log(string deviceName, string message)
        {
            string line = DateTime.Now.ToString("d/M/yyyy HH:mm:ss") + " [" + deviceName + "] " + message;
            WriteLine(line);
        }

        private void WriteLine(string line)
        {
            Console.WriteLine(line);
            Directory.CreateDirectory(_logsDirectory);
            string filePath = Path.Combine(_logsDirectory, DateTime.Now.ToString("yyyyMMdd") + ".txt");
            File.AppendAllText(filePath, line + Environment.NewLine);
        }
    }
}
