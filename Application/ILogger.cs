namespace MarcasZK.Application
{
    /// <summary>
    /// Application-level logging abstraction.
    /// Implementations live in Infrastructure.
    /// </summary>
    public interface ILogger
    {
        void Log(string message);
        void Log(string deviceName, string message);
    }
}
