using System;
using System.IO;
using System.Reflection;
using MarcasZK.Application;
using MarcasZK.Domain;
using MarcasZK.Infrastructure;

namespace MarcasZK
{
    /// <summary>
    /// Composition root: loads configuration, wires dependencies, and runs the export use case.
    /// Contains zero business logic — orchestration only.
    /// </summary>
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string configPath = Path.Combine(baseDir, "config.json");
                string outputDir = Path.GetFullPath(Path.Combine(baseDir, "marcas"));
                string logsDir = Path.Combine(baseDir, "logs");

                ILogger logger = new FileLogger(logsDir);

                string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                logger.Log("Inicio Version: " + version);

                Device[] devices = JsonConfigLoader.Load(configPath);
                logger.Log(string.Format("Cantidad de dispositivos: {0}", devices.Length));

                Directory.CreateDirectory(outputDir);

                IDeviceReader reader = new ZktecoDeviceReader();
                IMarkExporter exporter = new TxtMarkExporter();
                IDeviceCleaner cleaner = new ZktecoDeviceCleaner();
                ExportAttendanceMarksService service =
                    new ExportAttendanceMarksService(reader, exporter, logger, cleaner);

                int total = service.Execute(devices, outputDir);
                logger.Log(string.Format("Total marcas exportadas: {0}", total));

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fatal: {0}", ex.Message);
                return 1;
            }
        }
    }
}
