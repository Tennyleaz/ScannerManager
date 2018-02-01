using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Utility;
using Newtonsoft.Json;

namespace ScannerManager
{
    /// <summary>
    /// 兩個Scanner實例的比較，僅檢查ScannerType是否相同！
    /// </summary>
    public class Scanner : IEquatable<Scanner>, IComparable<Scanner>
    {
        public uint ConfigManagerErrorCode;
        public ValidScannerType ScannerType;
        public string ID;

        private bool _isWide;
        private string _deviceName;
        private string _friendlyName;
        private HashSet<string> supportedProductNames;
        private int priotity;

        [Obsolete("use another constructor!", true)]
        public Scanner(string name, ValidScannerType type, uint errorCode, string pidVidString)
        {
            /*_deviceName = name;
            ConfigManagerErrorCode = errorCode;
            ScannerType = type;
            supportedProductNames = new HashSet<string>();
            LoadScannerAttributes(pidVidString);*/
        }

        public Scanner(ValidScannerType type, ScannerJsonObject jsonObject, uint errorCode)
        {
            ScannerType = type;
            _deviceName = jsonObject.FriendlyName;
            _friendlyName = jsonObject.FriendlyName;
            ConfigManagerErrorCode = errorCode;
            priotity = jsonObject.Priority;
            ID = jsonObject.ID;
            _isWide = jsonObject.IsLandscape;
            supportedProductNames = new HashSet<string>(jsonObject.Support);            
        }

        /*public Scanner()
        {
            ScannerType = ValidScannerType.NotSupported;
            _deviceName = string.Empty;
            ConfigManagerErrorCode = 0;
        }*/

        /// <summary>
        /// 取得目前掃描器是橫向還是直向預覽
        /// </summary>
        public bool IsLandscape
        {
            get { return _isWide; }
        }

        public string FriendlyName
        {
            get
            {
                if (!string.IsNullOrEmpty(_friendlyName))
                    return _friendlyName;
                else if (!string.IsNullOrEmpty(_deviceName))
                    return _deviceName;
                else
                    return ScannerType.ToString();
            }
        }

        public int Priority { get { return priotity; } }

        public bool Equals(Scanner other)
        {
            if (other == null)
                return false;
            return (ScannerType == other.ScannerType && _friendlyName == other.FriendlyName);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return _friendlyName.GetHashCode() + ScannerType.GetHashCode();
            }
        }

        public bool IsProductSupported(string productName)
        {
            return supportedProductNames.Contains(productName);
        }

        private void LoadScannerAttributes(string pidVidString)
        {
            string jsonPath = Win32Message.IscanPath + ScannerType.ToString() + ".json";
            if (!File.Exists(jsonPath))
            {
                Logger.WriteLog("ScannerManager", LOG_LEVEL.LL_SERIOUS_ERROR, "Cannot find json file: " + ScannerType.ToString() + ".json");
                return;
            }
            // parse json file
            try
            {
                string jsonString = File.ReadAllText(jsonPath);
                ScannerJsonObject jsonObject = JsonConvert.DeserializeObject<ScannerJsonObject>(jsonString);
                // check vid pid 
                if (!jsonObject.CheckPidVid(pidVidString))
                {
                    Logger.WriteLog("ScannerManager", LOG_LEVEL.LL_SERIOUS_ERROR, "Mismatch PID/VID: " + pidVidString);
                }
                // load attributes from json object to scanner object
                _friendlyName = jsonObject.FriendlyName;
                supportedProductNames = new HashSet<string>(jsonObject.Support);
                priotity = jsonObject.Priority;
            }
            catch (Exception ex)
            {
                Logger.WriteLog("ScannerManager", LOG_LEVEL.LL_SERIOUS_ERROR, "Error parsing json file: " + ScannerType.ToString() + ".json, exception: " + ex);
            }
            return;
        }

        public int CompareTo(Scanner other)
        {
            if (other == null)
                return -1;  // -1代表this比other位置還要前面
            return -priotity.CompareTo(other.priotity);  // priority值越小，CompareTo回傳負數，所以我給他一個負號
        }
    }

    public class ScannerJsonObject : IComparable<ScannerJsonObject>, IEquatable<ScannerJsonObject>
    {        
        public string ScannerType { get; set; }
        public string FriendlyName { get; set; }
        public string USBName { get; set; }
        public string VID { get; set; }
        public string PID { get; set; }
        public string ID { get; set; }
        public int Priority { get; set; }
        public List<string> Support { get; set; }
        public bool IsLandscape { get; set; }

        public bool CheckPidVid(string deviceIDString)
        {
            if (string.IsNullOrEmpty(deviceIDString))
                return false;

            string spid = @"PID_" + PID;
            string svid = @"VID_" + VID;
            if (deviceIDString.Contains(spid) && deviceIDString.Contains(svid))
                return true;
            return false;
        }

        public ValidScannerType GetTypeEnum()
        {
            ValidScannerType t_out;
            if (Enum.TryParse(ScannerType, out t_out))
                return t_out;
            else
                return ValidScannerType.NotSupported;
        }

        /// <summary>
        /// 如果PID/VID不一樣，或是json檔案有問題，都會傳回null
        /// </summary>
        /// <param name="type"></param>
        /// <param name="pidVidString"></param>
        /// <returns></returns>
        public static ScannerJsonObject LoadFromFile(ValidScannerType type, string pidVidString)
        {
            string jsonPath = Win32Message.IscanPath + type.ToString() + ".json";
            if (!File.Exists(jsonPath))
            {
                Logger.WriteLog("ScannerManager", LOG_LEVEL.LL_SERIOUS_ERROR, "Cannot find json file: " + type.ToString() + ".json");
                return null;
            }
            // parse json file
            try
            {
                string jsonString = File.ReadAllText(jsonPath);
                ScannerJsonObject jsonObject = JsonConvert.DeserializeObject<ScannerJsonObject>(jsonString);
                // check vid pid 
                if (!jsonObject.CheckPidVid(pidVidString))
                {
                    Logger.WriteLog("ScannerManager", LOG_LEVEL.LL_SERIOUS_ERROR, "Mismatch PID/VID: " + pidVidString);
                    return null;
                }
                // 將obj的app用英文名排序
                jsonObject.Support.Sort();
                return jsonObject;
            }
            catch (Exception ex)
            {
                Logger.WriteLog("ScannerManager", LOG_LEVEL.LL_SERIOUS_ERROR, "Error parsing json file: " + type.ToString() + ".json, exception: " + ex);
            }
            return null;
        }

        public static ScannerJsonObject LoadFromFile(string fileFullapth)
        {
            if (!File.Exists(fileFullapth))
            {
                return null;
            }
            // parse json file
            try
            {
                string jsonString = File.ReadAllText(fileFullapth);
                ScannerJsonObject jsonObject = JsonConvert.DeserializeObject<ScannerJsonObject>(jsonString);
                // 將obj的app用英文名排序
                jsonObject.Support.Sort();
                // check vid pid 
                return jsonObject;
            }
            catch (Exception ex)
            {
                Logger.WriteLog("ScannerManager", LOG_LEVEL.LL_SERIOUS_ERROR, "Error parsing json file: " + fileFullapth + ", exception: " + ex);
            }
            return null;
        }

        public int CompareTo(ScannerJsonObject other)
        {
            if (other == null)
                return -1;
            return -Priority.CompareTo(other.Priority);
        }

        public bool Equals(ScannerJsonObject other)
        {
            if (other == null)
                return false;
            return PID.Equals(other.PID) && VID.Equals(other.VID);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return PID.GetHashCode() + VID.GetHashCode();
            }
        }
    }
    
}
