using System;

namespace MarcasZK.Domain
{
    /// <summary>
    /// Value Object: a single attendance mark collected from a biometric device.
    /// </summary>
    public class AttendanceMark
    {
        public string Origen { get; set; }
        public string EmployeeId { get; set; } // sanitized sdwEnrollNumber
        public DateTime Timestamp { get; set; }
        public string Flag { get; set; } // always "-"
    }
}
