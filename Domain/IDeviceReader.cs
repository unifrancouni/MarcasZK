namespace MarcasZK.Domain
{
    /// <summary>
    /// Port: abstracts reading attendance marks from a biometric device.
    /// Implementations live in Infrastructure; domain stays SDK-agnostic.
    /// </summary>
    public interface IDeviceReader
    {
        ReadMarksResult ReadMarks(Device device);
    }
}
