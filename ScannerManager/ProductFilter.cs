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
        private HashSet<App> appSet;
        private Matrix<int> baseMatrix;
        private Matrix<int> currentMatrix;
        private List<int> currentAppPriority;
        private Dictionary<string, int> appIndexDict;
        private Dictionary<string, int> scannerIndexDict;
        private int appCount, scannerCount;

        class App : IEquatable<App>
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
        }

        public static ProductFilter GetProducts()
        {
            return m_filter;
        }

        public ProductFilter()
        {
            InitProductList();
        }

        public bool IsAppMatchScanner(string productName, string scannerName)
        {
            return false;
        }

        private void InitProductList()
        {
            scannerJsonObjects = new SortedSet<ScannerJsonObject>();
            appSet = new HashSet<App>();
            try
            {
                var allJsonfiles = Directory.GetFiles(@"C:\Program Files (x86)\Penpower\iScan2\Bin\", "*json");
                for (int i = 0; i < allJsonfiles.Count(); i++)
                {
                    try
                    {
                        ScannerJsonObject obj = ScannerJsonObject.LoadFromFile(allJsonfiles[i]);
                        scannerJsonObjects.Add(obj);

                        // scanner name加入appSet內
                        foreach (var appName in obj.Support)
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
                        }
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

        /*public HashSet<string> GetNotifyApps(string newScannerUSBName, HashSet<string> currentScannerUSBNames)
        {
            HashSet<string> appset = new HashSet<string>();

            foreach (ScannerJsonObject scanner)

                return appset;
        }*/

        private void MakeMatrix()
        {
            // 讀取設定josn，判斷行列數量
            string jsonPath = @"C:\Program Files (x86)\Penpower\iScan2\Bin\AppRows.json";
            string jsonString = File.ReadAllText(jsonPath);
            JsonMatrix jsonMatrixObject = JsonConvert.DeserializeObject<JsonMatrix>(jsonString);
            scannerCount = jsonMatrixObject.ScannerRow.Count(); // scanner的數量
            appCount = jsonMatrixObject.AppColumn.Count();  // app的數量

            // 為每個app name建立字典，英文越前面index越小
            appIndexDict = new Dictionary<string, int>();
            for (int i=0; i< appCount; i++)
            {
                appIndexDict.Add(jsonMatrixObject.AppColumn[i], i);
            }

            // 為每個scanner type建立字典，priority越高index越小
            scannerIndexDict = new Dictionary<string, int>();
            int scannerIndex = 0;

            // 填滿每個ScannerRow的值
            List<int> oneRow = new List<int>();
            foreach (ScannerJsonObject scnobj in scannerJsonObjects)
            {
                // 填滿一個scanner row
                int[] iarray = new int[appCount];
                for(int i=0; i< appCount; i++)
                {
                    if (scnobj.Support.Contains(jsonMatrixObject.AppColumn[i]))
                        iarray[i] = 1;
                    else
                        iarray[i] = 0;
                }
                oneRow.AddRange(iarray);

                // 順便加上字典
                scannerIndexDict.Add(scnobj.ScannerType, scannerIndex);
                scannerIndex++;
            }
            
            // 宣告一個矩陣
            MatrixBuilder<int> builder = Matrix<int>.Build;
            baseMatrix = builder.DenseOfRowMajor(scannerCount, appCount, oneRow);
            // 複製一份到currentMatrix
            currentMatrix = builder.DenseOfMatrix(baseMatrix);
            // 基本的app priority
            currentAppPriority = new List<int>(scannerCount);
        }

        private HashSet<string> InsertScanner(string scannerType)
        {
            HashSet<string> resultAppNames = new HashSet<string>();

            // Get information about given scanner.
            ScannerJsonObject obj = scannerJsonObjects.FirstOrDefault(x => x.ScannerType == scannerType);
            if (obj == null)
                return null;
            int scannerRowIndex = scannerIndexDict[obj.ScannerType];
            int scannerPriority = obj.Priority;

            // 將scanner的priority乘上baseMatrix
            // 不會檢查是否已經乘過了！所以用baseMatrix乘，然後放回currentMatrix，可以避免。
            Vector<int> targetrRow = baseMatrix.Row(scannerRowIndex);
            targetrRow.Multiply(scannerPriority);
            currentMatrix.SetRow(scannerRowIndex, targetrRow);

            // 每個app column的結果，跟currentAppPriority比較
            for (int i=0; i<baseMatrix.ColumnCount; i++)
            {

            }

            // 如果變大，就加入resultAppNames，並更新跟currentAppPriority比較

            return resultAppNames;
        }

        class JsonMatrix
        {
            /// <summary>
            /// Scanner利用priority來排序，越高越前面
            /// </summary>
            public List<string> ScannerRow { get; set; }
            /// <summary>
            /// App利用英文名稱來排序，越小越前面
            /// </summary>
            public List<string> AppColumn { get; set; }
        }
    }
}
