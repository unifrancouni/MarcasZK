namespace MarcasZK.Domain
{
    /// <summary>
    /// Result type: outcome of clearing attendance logs from a single device.
    /// </summary>
    public class ClearMarksResult
    {
        public bool Success { get; set; }
        public int ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
    }
}
