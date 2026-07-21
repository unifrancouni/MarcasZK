namespace MarcasZK.Domain
{
    /// <summary>
    /// Port: abstracts persisting a single attendance mark to an output destination.
    /// Implementations live in Infrastructure; domain stays filesystem-agnostic.
    /// </summary>
    public interface IMarkExporter
    {
        void Export(AttendanceMark mark, string outputDirectory);
    }
}
