using System;
using System.IO;

namespace ArgosMonitor.Functions
{
    public class OutputToFile
    {
        private readonly string _logDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? string.Empty, "logs");

        private static OutputToFile _outputToFileSingleton;
        private static OutputToFile outputToFileSingleton
        {
            get { return _outputToFileSingleton ??= new OutputToFile(); }
        }

        public StreamWriter  sw { get; set; }

        public OutputToFile()
        {
            EnsureLogDirectoryExists();
            InstantiateStreamWriter();
        }

        ~OutputToFile()
        {
            if (sw == null) return;
            try
            {
                sw.Dispose();
            }
            catch (ObjectDisposedException) { } // object already disposed - ignore exception
        }

        public static void WriteLine(string str)
        {
            Console.WriteLine(str);
            outputToFileSingleton.sw.WriteLine("\n" + str);
        }

        public static void Write(string str)
        {
            Console.Write(str);
            outputToFileSingleton.sw.Write(str);
        }

        private void InstantiateStreamWriter()
        {
            var filePath = Path.Combine(_logDirPath, DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")) + ".txt";
            try
            {
                sw = new StreamWriter(filePath)
                {
                    AutoFlush = true
                };
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new ApplicationException(
                    $"Access denied. Could not instantiate StreamWriter using path: {filePath}.", ex);
            }
        }

        private void EnsureLogDirectoryExists()
        {
            if (Directory.Exists(_logDirPath)) return;

            try
            {
                Directory.CreateDirectory(_logDirPath);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new ApplicationException(
                    $"Access denied. Could not create log directory using path: {_logDirPath}.", ex);
            }
        }
    }
}
