namespace MarcasZK.Domain
{
    /// <summary>
    /// Value Object: network connection parameters for a ZKTeco biometric device.
    /// </summary>
    public class DeviceConnection
    {
        public string Ip { get; set; }
        public int Port { get; set; }
        public string Password { get; set; } // empty string = no password
    }
}
