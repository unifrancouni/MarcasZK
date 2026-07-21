using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MarcasZK.Domain;
using zkemkeeper;

namespace MarcasZK.Infrastructure
{
    /// <summary>
    /// Implements IDeviceReader using zkemkeeper COM interop.
    /// Connection flow:
    ///   1. Instantiate CZKEMClass
    ///   2. Call SetCommPasswordEx (always — SDK handles empty string)
    ///   3. Connect_Net(ip, port) — return failure result if false
    ///   4. EnableDevice(1, false)
    ///   5. ReadGeneralLogData
    ///   6. SSR_GetGeneralLogData loop to collect marks
    ///   7. EnableDevice(1, true) + Disconnect in finally (guaranteed)
    /// </summary>
    public class ZktecoDeviceReader : IDeviceReader
    {
        private const int MachineNumber = 1;

        public ReadMarksResult ReadMarks(Device device)
        {
            CZKEMClass sdk = null;
            bool connected = false;

            try
            {
                sdk = new CZKEMClass();

                // Always call SetCommPasswordEx — SDK handles empty string gracefully
                sdk.SetCommPasswordEx(device.Connection.Password);

                bool bResult = sdk.Connect_Net(device.Connection.Ip, device.Connection.Port);
                if (!bResult)
                {
                    int errorCode = 0;
                    sdk.GetLastError(ref errorCode);
                    return new ReadMarksResult
                    {
                        Success = false,
                        ErrorType = ReadErrorType.Connection,
                        ErrorMessage = string.Format("connection failed ({0}:{1})",
                            device.Connection.Ip, device.Connection.Port),
                        ErrorCode = errorCode
                    };
                }

                connected = true;
                sdk.EnableDevice(MachineNumber, false);

                List<AttendanceMark> marks = new List<AttendanceMark>();

                if (sdk.ReadGeneralLogData(MachineNumber))
                {
                    string sdwEnrollNumber = string.Empty;
                    int idwVerifyMode = 0;
                    int idwInOutMode = 0;
                    int idwYear = 0;
                    int idwMonth = 0;
                    int idwDay = 0;
                    int idwHour = 0;
                    int idwMinute = 0;
                    int idwSecond = 0;
                    int idwWorkCode = 0;

                    while (sdk.SSR_GetGeneralLogData(
                        MachineNumber,
                        out sdwEnrollNumber,
                        out idwVerifyMode,
                        out idwInOutMode,
                        out idwYear,
                        out idwMonth,
                        out idwDay,
                        out idwHour,
                        out idwMinute,
                        out idwSecond,
                        ref idwWorkCode))
                    {
                        marks.Add(new AttendanceMark
                        {
                            Origen = device.Origen,
                            EmployeeId = SanitizeEmployeeId(sdwEnrollNumber),
                            Timestamp = new DateTime(idwYear, idwMonth, idwDay,
                                                     idwHour, idwMinute, idwSecond),
                            Flag = "-"
                        });
                    }
                }
                else
                {
                    int errorCode = 0;
                    sdk.GetLastError(ref errorCode);

                    // Error 0 or -4991: SDK returns false when there are no logs — not a real error
                    if (errorCode == 0 || errorCode == -4991)
                    {
                        return new ReadMarksResult
                        {
                            Success = true,
                            Marks = marks
                        };
                    }

                    return new ReadMarksResult
                    {
                        Success = false,
                        ErrorType = ReadErrorType.ReadData,
                        ErrorMessage = "ReadGeneralLogData failed",
                        ErrorCode = errorCode
                    };
                }

                return new ReadMarksResult
                {
                    Success = true,
                    ErrorMessage = null,
                    Marks = marks
                };
            }
            catch (COMException comEx)
            {
                return new ReadMarksResult
                {
                    Success = false,
                    ErrorType = ReadErrorType.ComNotRegistered,
                    ErrorMessage = "SDK COM not registered — run Register_SDK x64.bat. Detail: " + comEx.Message
                };
            }
            catch (Exception ex)
            {
                return new ReadMarksResult
                {
                    Success = false,
                    ErrorType = ReadErrorType.Unexpected,
                    ErrorMessage = ex.Message
                };
            }
            finally
            {
                if (sdk != null && connected)
                {
                    try { sdk.EnableDevice(MachineNumber, true); } catch { }
                    try { sdk.Disconnect(); } catch { }
                }
            }
        }

        private static string SanitizeEmployeeId(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return raw;

            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            string result = raw;
            foreach (char c in invalid)
                result = result.Replace(c.ToString(), string.Empty);

            return result;
        }
    }
}
