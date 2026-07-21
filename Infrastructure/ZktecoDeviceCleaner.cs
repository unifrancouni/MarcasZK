using System;
using System.Runtime.InteropServices;
using MarcasZK.Domain;
using zkemkeeper;

namespace MarcasZK.Infrastructure
{
    /// <summary>
    /// Implements IDeviceCleaner using zkemkeeper COM interop.
    /// Connection flow: SetCommPasswordEx → Connect_Net → EnableDevice(false) →
    /// ClearGLog → RefreshData (only on success) → EnableDevice(true) → Disconnect.
    /// EnableDevice(true) and Disconnect are finally-guaranteed.
    /// </summary>
    public class ZktecoDeviceCleaner : IDeviceCleaner
    {
        private const int MachineNumber = 1;

        public ClearMarksResult ClearMarks(Device device)
        {
            CZKEMClass sdk = null;
            bool connected = false;
            ClearMarksResult result = new ClearMarksResult();

            try
            {
                sdk = new CZKEMClass();
                sdk.SetCommPasswordEx(device.Connection.Password);

                if (!sdk.Connect_Net(device.Connection.Ip, device.Connection.Port))
                {
                    int errorCode = 0;
                    sdk.GetLastError(ref errorCode);
                    result.Success = false;
                    result.ErrorCode = errorCode;
                    result.ErrorMessage = string.Format(
                        "ClearMarks connection failed ({0}:{1})",
                        device.Connection.Ip, device.Connection.Port);
                    return result;
                }

                connected = true;
                sdk.EnableDevice(MachineNumber, false);

                if (sdk.ClearGLog(MachineNumber))
                {
                    sdk.RefreshData(MachineNumber);
                    result.Success = true;
                }
                else
                {
                    int errorCode = 0;
                    sdk.GetLastError(ref errorCode);
                    result.Success = false;
                    result.ErrorCode = errorCode;
                    result.ErrorMessage = string.Format("ClearGLog failed, error={0}", errorCode);
                }
            }
            catch (COMException comEx)
            {
                result.Success = false;
                result.ErrorMessage = "SDK COM error during ClearMarks: " + comEx.Message;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = "Unexpected error during ClearMarks: " + ex.Message;
            }
            finally
            {
                if (sdk != null && connected)
                {
                    try { sdk.EnableDevice(MachineNumber, true); } catch { }
                    try { sdk.Disconnect(); } catch { }
                }
            }

            return result;
        }
    }
}
