using System.IO;
using MarcasZK.Domain;

namespace MarcasZK.Infrastructure
{
    /// <summary>
    /// Implements IMarkExporter — writes one TXT file per attendance mark in Oracle-importable format.
    ///
    /// Filename: {origen}-{yyyyMMddHHmmss}-{employeeId}.txt
    /// Content:  '{origen}','{employeeId}', TO_DATE('{yyyy/MM/dd HH:mm:ss}', 'yyyy/mm/dd hh24:mi:ss'),'-'
    ///
    /// File collision: silently overwritten (business rule).
    /// EmployeeId is pre-sanitized by ZktecoDeviceReader; no double-sanitization needed.
    /// </summary>
    public class TxtMarkExporter : IMarkExporter
    {
        public void Export(AttendanceMark mark, string outputDirectory)
        {
            string timestamp = mark.Timestamp.ToString("yyyyMMddHHmmss");
            string filename = string.Format("{0}-{1}-{2}.txt",
                mark.Origen, timestamp, mark.EmployeeId);

            string filePath = Path.Combine(outputDirectory, filename);

            string oracleTimestamp = mark.Timestamp.ToString("yyyy/MM/dd HH:mm:ss");
            string content = string.Format(
                "'{0}','{1}', TO_DATE('{2}', 'yyyy/mm/dd hh24:mi:ss'),'-'",
                mark.Origen,
                mark.EmployeeId,
                oracleTimestamp);

            // Overwrites on collision — intentional per spec
            File.WriteAllText(filePath, content);
        }
    }
}
