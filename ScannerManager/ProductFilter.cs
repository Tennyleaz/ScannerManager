using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Utility;
using Newtonsoft.Json;
using MathNet.Numerics.LinearAlgebra;

namespace ScannerManager
{
    internal class ProductFilter
    {
        private static ProductFilter m_filter;
        private SortedSet<ScannerJsonObject> scannerJsonObjects;
        //private HashSet<App> appSet;                       // 儲存所有支援的App
        private HashSet<string> connectedAppSet;           // 目前已經連線的App
        private Matrix<float> baseMatrix;                  // 紀錄scanner-app支援關係的矩陣
        private Matrix<float> currentMatrix;               // 目前已連接的掃描器優先度矩陣
        private List<float> currentAppPriority;            // 目前所有支援的App，已連線的掃描器優先度
        private Dictionary<string, int> appIndexDict;      // 用來查詢所有App的index
        private Dictionary<int, string> appNameDict;       // 用來查詢所有App的名稱
        private Dictionary<string, int> scannerIndexDict;  // 用來查詢所有掃描器的index
        private int appCount, scannerCount;

        /*class App : IEquatable<App>
        {
            public string ProductName;
            public HashSet<ScannerJsonObject> SupportedScanners = new HashSet<ScannerJsonObject>();

            public bool Equals(App other)
            {
                if (other == null) return false;
                return ProductName == other.ProductName;
            }

            public override int GetHashCode()
            {
                return ProductName.GetHashCode();
            }
        }*/

        class JsonMatrix
        {
            /// <summary>
            /// Scanner利用priority來排序，越高越前面
            /// </summary>
            public List<string> ScannerTypes { get; set; }
            /// <summary>
            /// App利用英文名稱來排序，越小越前面
            /// </summary>
            public List<string> AppNames { get; set; }
        }

        /// <summary>
        /// Need this method to access functions in ProductFilter class. Call Init() first.
        /// </summary>
        /// <returns></returns>
        public static ProductFilter GetProducts()
        {
            return m_filter;
        }

        public static void Init()
        {
            if (m_filter == null)
                m_filter = new ProductFilter();
        }

        public static void UnInit()
        {
            m_filter = null;
        }

        public ProductFilter()
        {
            InitProductList();
            MakeMatrix();
            connectedAppSet = new HashSet<string>();
        }

        /// <summary>
        /// 每次有新App連線就要記錄到connectedAppSet
        /// </summary>
        /// <param name="productName"></param>
        public void AddApp(string productName)
        {
            if (string.IsNullOrEmpty(productName)) return;
            connectedAppSet.Add(productName);
        }

        /// <summary>
        /// 每次有App斷線就要從connectedAppSet移除
        /// </summary>
        /// <param name="productName"></param>
        public void RemoveApp(string productName)
        {
            if (string.IsNullOrEmpty(productName)) return;
            if (connectedAppSet.Contains(productName))
                connectedAppSet.Remove(productName);
        }

        /// <summary>
        /// 利用PID/VID尋找第一個相同的ScannerJsonObject，沒找到就回傳null
        /// </summary>
        /// <param name="deviceID"></param>
        /// <returns></returns>
        public ScannerJsonObject LoadObjectByDeviceID(string deviceID)
        {
            if (m_filter == null || scannerJsonObjects == null || deviceID == null)
                return null;
            foreach (ScannerJsonObject obj in scannerJsonObjects)
            {
                if (obj.CheckPidVid(deviceID))
                    return obj;
            }
            return null;
            /*string PID, VID;
            var ids= Extract_VIP_PID(deviceID);
            if (ids == null)
                return null;
            VID = ids.Item1;
            PID = ids.Item2;
            return scannerJsonObjects.FirstOrDefault(x => (x.PID == PID && x.VID == VID));*/
        }

        /// <summary>
        /// 檢查是否是相容的PID/VID，需要先init過才能用
        /// </summary>
        /// <param name="deviceID"></param>
        /// <returns></returns>
        public bool IsDeviceIDValid(string deviceID)
        {
            if (m_filter == null || scannerJsonObjects == null || deviceID == null)
                return false;
            /*string PID, VID;
            var ids = Extract_VIP_PID(deviceID);
            if (ids == null)
                return false;
            VID = ids.Item1;
            PID = ids.Item2;*/
            // 檢查scannerJsonObjects有沒有這個ID
            var matches = scannerJsonObjects.FirstOrDefault(s => s.CheckPidVid(deviceID));
            if (matches != null)
                return true;
            else
                return false;
        }

        public ValidScannerType DeviceIDToScannerType(string deviceID)
        {
            if (string.IsNullOrEmpty(deviceID))
                return ValidScannerType.NotSupported;

            ScannerJsonObject obj = LoadObjectByDeviceID(deviceID);
            if (obj == null)
                return ValidScannerType.NotSupported;

            ValidScannerType resultType;
            if (Enum.TryParse(obj.ScannerType, out resultType))
                return resultType;
            else
                return ValidScannerType.NotSupported;
        }

        /// <summary>
        /// Extract VID and PID. In the return tuple, Item1 is VID, Item2 is PID.
        /// </summary>
        /// <param name="deviceID"></param>
        /// <returns></returns>
        private Tuple<string, string> Extract_VIP_PID(string deviceID)
        {
            if (string.IsNullOrEmpty(deviceID))
                return null;

            string PID = string.Empty, VID = string.Empty;
            int index = deviceID.IndexOf("VID_");
            if (index >= 0)
            {
                VID = deviceID.Substring(index + 4, 4);
            }
            index = deviceID.IndexOf("PID_");
            if (index >= 0)
            {
                PID = deviceID.Substring(index + 4, 4);
            }
            if (string.IsNullOrEmpty(PID) || string.IsNullOrEmpty(VID))
                return null;

            return Tuple.Create(VID, PID);
        }

        private void InitProductList()
        {
            scannerJsonObjects = new SortedSet<ScannerJsonObject>();
            //appSet = new HashSet<App>();
            try
            {
                string targetDir = Win32Message.IscanPath;
                var allJsonfiles = Directory.GetFiles(targetDir, "*json");
                for (int i = 0; i < allJsonfiles.Count(); i++)
                {
                    try
                    {
                        ScannerJsonObject obj = ScannerJsonObject.LoadFromFile(allJsonfiles[i]);
                        if (obj == null)
                            continue;
                        scannerJsonObjects.Add(obj);

                        // scanner name加入appSet內
                        /*foreach (var appName in obj.Support)
                        {
                            var supportedApps = appSet.Where(a => a.ProductName == appName);
                            // 如果沒這個app先新增一個
                            if (supportedApps == null || supportedApps.Count() == 0)
                            {
                                App newApp = new App();
                                appSet.Add(newApp);
                            }
                            // 然後每個app內，SupportedScanners都增加此ScannerJsonObject的USBName
                            supportedApps = appSet.Where(a => a.ProductName == appName);
                            if (supportedApps != null && supportedApps.Count() > 0)
                            {
                                foreach(App app in supportedApps)
                                {
                                    app.SupportedScanners.Add(obj);
                                }
                            }
                        }*/
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLog("ProductFilter", LOG_LEVEL.LL_SERIOUS_ERROR, "InitProductList() exception: " + ex);
            }
        }

        public HashSet<string> GetSupportedApps(string scannerUSBName)
        {
            HashSet<string> appset = new HashSet<string>();
            var validScanners = scannerJsonObjects.Where(x => x.USBName == scannerUSBName);
            foreach (ScannerJsonObject scnobj in validScanners)
            {
                if (scnobj.Support != null)
                    appset.UnionWith(scnobj.Support);
            }
            return appset;
        }

        private void MakeMatrix()
        {
            // 讀取設定josn，判斷行列數量
            string jsonPath = Win32Message.IscanPath + @"AppRows.filter";
            string jsonString = File.ReadAllText(jsonPath);
            JsonMatrix jsonMatrixObject = JsonConvert.DeserializeObject<JsonMatrix>(jsonString);
            scannerCount = jsonMatrixObject.ScannerTypes.Count(); // scanner的數量
            appCount = jsonMatrixObject.AppNames.Count();  // app的數量

            // 為每個app name建立字典，英文越前面index越小，另外建立一個index->name的字典
            appIndexDict = new Dictionary<string, int>();
            appNameDict = new Dictionary<int, string>();
            for (int i = 0; i < appCount; i++)
            {
                appIndexDict.Add(jsonMatrixObject.AppNames[i], i);
                appNameDict.Add(i, jsonMatrixObject.AppNames[i]);
            }

            // 為每個scanner type建立字典，priority越高index越小
            scannerIndexDict = new Dictionary<string, int>();
            int scannerIndex = 0;

            // 填滿每個ScannerRow的值
            List<float> oneRow = new List<float>();
            foreach (ScannerJsonObject scnobj in scannerJsonObjects)
            {
                // 填滿一個scanner row
                float[] iarray = new float[appCount];
                for (int i = 0; i < appCount; i++)
                {
                    if (scnobj.Support.Contains(jsonMatrixObject.AppNames[i]))
                        iarray[i] = 1;
                    else
                        iarray[i] = 0;
                }
                oneRow.AddRange(iarray);

                // 順便加上字典，這裡假設每個不同的掃描器有獨立的ScannerType
                scannerIndexDict.Add(scnobj.ScannerType, scannerIndex);
                scannerIndex++;
            }

            // 宣告一個矩陣
            MatrixBuilder<float> builder = Matrix<float>.Build;
            baseMatrix = builder.DenseOfRowMajor(scannerCount, appCount, oneRow);
            // 複製一份到currentMatrix
            currentMatrix = builder.DenseOfMatrix(baseMatrix);

            // 基本的app priority，長度必須先建立好
            currentAppPriority = new List<float>(new float[appCount]);
        }

        public HashSet<string> InsertScanner(ValidScannerType scannerType)
        {
            HashSet<string> resultAppNames = new HashSet<string>();

            // Get information about given scanner.
            ScannerJsonObject obj = scannerJsonObjects.FirstOrDefault(x => x.ScannerType == scannerType.ToString());
            if (obj == null)
                return null;
            int scannerRowIndex = scannerIndexDict[obj.ScannerType];
            int scannerPriority = obj.Priority;

            // 將scanner的priority乘上baseMatrix
            // 不會檢查是否已經乘過了！所以用baseMatrix乘，然後放回currentMatrix，可以避免。
            Vector<float> targetrRow = baseMatrix.Row(scannerRowIndex);
            targetrRow = targetrRow.Multiply(scannerPriority);
            currentMatrix.SetRow(scannerRowIndex, targetrRow);

            // 每個app column的結果，跟currentAppPriority比較
            Vector<float> mask = baseMatrix.Row(scannerRowIndex);  // 用來判斷此row的App是否支援此掃描器
            for (int i = 0; i < appCount; i++)
            {
                Vector<float> newColumn = currentMatrix.Column(i);                
                if (mask[i] > 0)
                {
                    float newValue = newColumn.Maximum();
                    if (newValue > currentAppPriority[i] && mask[i] > 0)
                    {
                        // 發現需要替換目標掃描器的app，就加入resultAppNames
                        resultAppNames.Add(appNameDict[i]);
                        // 更新目前app的priority
                        currentAppPriority[i] = newValue;
                    }
                }
            }

            // 過濾剩下已連線的App
            resultAppNames.IntersectWith(connectedAppSet);
            return resultAppNames;
        }

        /// <summary>
        /// First hashset are the apps need to notify add.
        /// 2nd hashset are the apps need to notify removed.
        /// </summary>
        /// <param name="scannerType"></param>
        /// <returns></returns>
        public Tuple<HashSet<string>, HashSet<string>> RemoveScanner(ValidScannerType scannerType)
        {
            HashSet<string> updatedAppNames = new HashSet<string>();
            HashSet<string> unpluggedAppNames = new HashSet<string>();

            // Get information about given scanner.
            ScannerJsonObject obj = scannerJsonObjects.FirstOrDefault(x => x.ScannerType == scannerType.ToString());
            if (obj == null)
                return null;
            int scannerRowIndex = scannerIndexDict[obj.ScannerType];
            int scannerPriority = obj.Priority;

            // 將currentMatrix的相對列設為1
            Vector<float> targetrRow = baseMatrix.Row(scannerRowIndex);
            currentMatrix.SetRow(scannerRowIndex, targetrRow);

            // 每個app column的結果，跟currentAppPriority比較
            Vector<float> mask = baseMatrix.Row(scannerRowIndex);  // 用來判斷此row的App是否支援此掃描器
            for (int i = 0; i < appCount; i++)
            {
                Vector<float> newColumn = currentMatrix.Column(i);
                if (mask[i] > 0)
                {
                    float newValue = newColumn.Max();
                    if (newValue == 1 && currentAppPriority[i] > 1)
                    {
                        // =1但是舊值>1，代表沒有符合掃描器了，僅需要回報移除
                        unpluggedAppNames.Add(appNameDict[i]);
                    }
                    else if (newValue > 1 && newValue < currentAppPriority[i])
                    {
                        // >1但是小於舊值，發現需要替換目標掃描器的app，就加入resultAppNames
                        updatedAppNames.Add(appNameDict[i]);
                    }
                    // 更新目前app的priority  
                    currentAppPriority[i] = newValue;
                }
            }

            // 過濾剩下已連線的App
            updatedAppNames.IntersectWith(connectedAppSet);
            unpluggedAppNames.IntersectWith(connectedAppSet);
            return Tuple.Create(updatedAppNames, unpluggedAppNames);
        }

        public int GetSupportedScannerCount(string productName)
        {
            // 檢查baseMatrix，找出app那一欄內有幾個1 (即所有數字加總)
            int targetColumn = appIndexDict[productName];
            var column = baseMatrix.Column(targetColumn);
            int sum = (int)column.Sum();
            return sum;
        }
    }
}
