using System.Collections.Generic;

namespace MarcasZK.Domain
{
    /// <summary>
    /// Classifies the type of failure when reading marks from a device.
    /// </summary>
    public enum ReadErrorType
    {
        None,
        Connection,
        ReadData,
        NoData,
        ComNotRegistered,
        Unexpected
    }

    /// <summary>
    /// Result type: outcome of reading attendance marks from a single device.
    /// Carries either the collected marks on success, or an error message on failure.
    /// </summary>
    public class ReadMarksResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public int ErrorCode { get; set; }
        public ReadErrorType ErrorType { get; set; }
        public List<AttendanceMark> Marks { get; set; }

        public ReadMarksResult()
        {
            Marks = new List<AttendanceMark>();
            ErrorType = ReadErrorType.None;
        }
    }
}
