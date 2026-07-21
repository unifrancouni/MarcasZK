namespace MarcasZK.Domain
{
    /// <summary>
    /// Entity: a configured biometric device with its connection parameters and export settings.
    /// </summary>
    public class Device
    {
        public string Origen { get; set; }
        public string Nombre { get; set; }
        public DeviceConnection Connection { get; set; }
        public bool BorrarMarcas { get; set; } // parsed from config and acted upon by ExportAttendanceMarksService
    }
}
