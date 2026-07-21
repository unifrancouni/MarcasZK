using System;
using MarcasZK.Domain;

namespace MarcasZK.Application
{
    /// <summary>
    /// Use case: export attendance marks from all configured devices.
    /// Orchestrates IDeviceReader and IMarkExporter — contains zero infrastructure dependencies.
    /// </summary>
    public class ExportAttendanceMarksService
    {
        private readonly IDeviceReader _reader;
        private readonly IMarkExporter _exporter;
        private readonly ILogger _logger;
        private readonly IDeviceCleaner _cleaner;

        public ExportAttendanceMarksService(
            IDeviceReader reader, IMarkExporter exporter, ILogger logger,
            IDeviceCleaner cleaner = null)
        {
            _reader = reader;
            _exporter = exporter;
            _logger = logger;
            _cleaner = cleaner;
        }

        /// <summary>
        /// Processes all devices sequentially. Never throws — catches per-device errors and logs them.
        /// Returns the total number of marks exported across all devices.
        /// </summary>
        public int Execute(Device[] devices, string outputDirectory)
        {
            int total = 0;

            foreach (Device device in devices)
            {
                try
                {
                    _logger.Log(device.Nombre, "Inicio");
                    _logger.Log(device.Nombre,
                        string.Format("Buscando dispositivo {0}:{1}",
                            device.Connection.Ip, device.Connection.Port));

                    ReadMarksResult result = _reader.ReadMarks(device);

                    if (!result.Success)
                    {
                        switch (result.ErrorType)
                        {
                            case ReadErrorType.Connection:
                                _logger.Log(device.Nombre,
                                    string.Format("No fue posible conectar el dispositivo, Código Error={0} ({1})",
                                        result.ErrorCode, TranslateErrorCode(result.ErrorCode)));
                                break;
                            case ReadErrorType.ReadData:
                                _logger.Log(device.Nombre,
                                    string.Format("Dispositivo conectado pero falló la lectura de datos, Código Error={0} ({1})",
                                        result.ErrorCode, TranslateErrorCode(result.ErrorCode)));
                                break;
                            case ReadErrorType.ComNotRegistered:
                                _logger.Log(device.Nombre,
                                    "Error COM: " + result.ErrorMessage);
                                break;
                            default:
                                _logger.Log(device.Nombre,
                                    "Error inesperado: " + result.ErrorMessage);
                                break;
                        }
                        continue;
                    }

                    _logger.Log(device.Nombre, "Dispositivo conectado");
                    _logger.Log(device.Nombre, "Inicia Descarga");

                    int deviceCount = 0;
                    bool exportSucceeded = false;

                    try
                    {
                        foreach (AttendanceMark mark in result.Marks)
                        {
                            _exporter.Export(mark, outputDirectory);
                            deviceCount++;
                        }
                        exportSucceeded = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(device.Nombre,
                            string.Format("Error exportando marcas: {0}", ex.Message));
                    }

                    if (deviceCount > 0)
                    {
                        _logger.Log(device.Nombre,
                            string.Format("Fin Descarga - {0} marcas descargadas", deviceCount));
                        _logger.Log(device.Nombre,
                            string.Format("{0} marcas guardadas", deviceCount));
                    }
                    else
                    {
                        _logger.Log(device.Nombre, "Fin Descarga - No hay datos nuevos");
                    }

                    total += deviceCount;

                    if (exportSucceeded && deviceCount > 0 && device.BorrarMarcas && _cleaner != null)
                    {
                        ClearMarksResult clearResult = _cleaner.ClearMarks(device);
                        if (clearResult.Success)
                        {
                            _logger.Log(device.Nombre,
                                string.Format("{0} marcas eliminadas del dispositivo", deviceCount));
                        }
                        else
                        {
                            _logger.Log(device.Nombre,
                                string.Format("Error eliminando marcas: código={0}, {1}",
                                    clearResult.ErrorCode, clearResult.ErrorMessage));
                        }
                    }
                    else if (device.BorrarMarcas && _cleaner == null)
                    {
                        _logger.Log(device.Nombre,
                            "Advertencia: borrarMarcas=true pero limpiador no configurado");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(device.Nombre, "Error inesperado: " + ex.Message);
                }
            }

            return total;
        }

        private static string TranslateErrorCode(int errorCode)
        {
            switch (errorCode)
            {
                case 0: return "sin datos";
                case -1: return "SDK no inicializado";
                case -2: return "error de conexión de red";
                case -6: return "contraseña incorrecta";
                case -8: return "timeout de recepción";
                case -307: return "timeout de conexión";
                default: return "código desconocido";
            }
        }
    }
}
