using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Windows;

namespace Utility
{
    public class DriverTester
    {
        private const string A6_NAME = "A6 Scanner";          // 裝置管理員顯示的名稱
        private const string A8_NAME = "A8 ColorScanner PP";  // 裝置管理員顯示的名稱
        private const string Manufacturer = "PenPower";

        public static void ReadScannerDrivers()
        {
            Logger.WriteLog("DriverInfo", LOG_LEVEL.LL_NORMAL_LOG, "=== Get startup driver info ===");
            try
            {
                ManagementObjectSearcher objSearcher = new ManagementObjectSearcher("Select * from Win32_PnPSignedDriver");
                ManagementObjectCollection objCollection = objSearcher.Get();

                foreach (ManagementObject obj in objCollection)
                {
                    // Check for device name
                    string deviceName = Convert.ToString(obj["DeviceName"]);
                    string manufacturerName = Convert.ToString(obj["Manufacturer"]);
                    if (A6_NAME.Equals(deviceName) || A8_NAME.Equals(deviceName))                    
                    {
                        string info = String.Format("Device={0}, Manufacturer={1}, DriverVersion={2}, DriverDate={3}, DeviceClass={4}",
                            deviceName, manufacturerName, obj["DriverVersion"], obj["DriverDate"], obj["DeviceClass"]);
                        
                        Logger.WriteLog("DriverInfo", LOG_LEVEL.LL_NORMAL_LOG, info);

                        string driverVersion = Convert.ToString(obj["DriverVersion"]);
                        if (string.IsNullOrEmpty(driverVersion) || string.IsNullOrEmpty(manufacturerName))
                        {
                            string errorString = "Error:\nCannot detect a proper driver for: " + deviceName;
                            MessageBox.Show(errorString, "Scannner Manager");
                        }
                    }
                }

                objSearcher.Dispose();
                objCollection.Dispose();
            }
            catch (Exception ex)
            {
                Logger.WriteLog("DriverInfo", LOG_LEVEL.LL_SUB_FUNC, "Get startup driver info exception: " + ex);
            }
            Logger.WriteLog("DriverInfo", LOG_LEVEL.LL_NORMAL_LOG, "Get startup driver info...done.");
        }

        public static ScannerDriverState CheckGivenScannerHasDriver(string targetScannerName)
        {
            if (string.IsNullOrEmpty(targetScannerName))
                return ScannerDriverState.NoSuchScanner;

            ScannerDriverState state = ScannerDriverState.NoSuchScanner;
            try
            {
                ManagementObjectSearcher objSearcher = new ManagementObjectSearcher("Select * from Win32_PnPSignedDriver");
                ManagementObjectCollection objCollection = objSearcher.Get();

                foreach (ManagementObject obj in objCollection)
                {
                    // Check for device name
                    string deviceName = Convert.ToString(obj["DeviceName"]);
                    string manufacturerName = Convert.ToString(obj["Manufacturer"]);
                    if (targetScannerName.Equals(deviceName))
                    {
                        string info = String.Format("Checking device {0}: Manufacturer={1}, DriverVersion={2}, DriverDate={3}, DeviceClass={4}",
                            deviceName, manufacturerName, obj["DriverVersion"], obj["DriverDate"], obj["DeviceClass"]);

                        Logger.WriteLog("DriverInfo", LOG_LEVEL.LL_NORMAL_LOG, info);

                        // Check for driver version / manufacturer name
                        string driverVersion = Convert.ToString(obj["DriverVersion"]);
                        if (string.IsNullOrEmpty(driverVersion) || string.IsNullOrEmpty(manufacturerName))
                        {
                            string errorString = "Cannot detect a proper driver for: " + deviceName;
                            Logger.WriteLog("DriverInfo", LOG_LEVEL.LL_SUB_FUNC, errorString);
                            state =  ScannerDriverState.CantFindDriver;
                            MessageBox.Show(errorString, "Scannner Manager");
                            break;
                        }
                        else
                        {
                            state = ScannerDriverState.HasDriver;
                            break;
                        }
                        break;
                    }
                }

                objSearcher.Dispose();
                objCollection.Dispose();
            }
            catch (Exception ex)
            {
                Logger.WriteLog("DriverInfo", LOG_LEVEL.LL_SUB_FUNC, "CheckGivenScannerHasDriver() exception: " + ex);
            }
            return state;
        }
    }

    public enum ScannerDriverState
    {
        NoSuchScanner,
        CantFindDriver,
        HasDriver
    }
}
