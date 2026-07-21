namespace MarcasZK.Domain
{
    /// <summary>
    /// Port: abstracts clearing attendance logs from a biometric device.
    /// Implementations live in Infrastructure; domain stays SDK-agnostic.
    /// </summary>
    public interface IDeviceCleaner
    {
        ClearMarksResult ClearMarks(Device device);
    }
}
