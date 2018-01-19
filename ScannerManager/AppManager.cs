using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Utility;

namespace ScannerManager
{
    public class AppManager
    {
        private const string WIN_MESSAGE = "SCANMANGERX";
        private static HashSet<ClientApp> clientApps = new HashSet<ClientApp>();
        private uint MessageIdentifier;
        private Dictionary<string, string> connectionNameDict = new Dictionary<string, string>();  // key:product name, vlaue:connection name
        //private ClientApp currentApp;

        /// <summary>
        /// 判斷前景的App是不是已經紀錄的目標App。null代表沒有app，空白名稱的ClientApp代表前景App不符合任何有紀錄的目標。
        /// </summary>
        public static ClientApp TargetApp
        {
            get
            {
                if (clientApps.Count == 0)
                    return null;
                else
                {
                    // 尋找clientApps裡面，誰在前景，並回傳
                    // 先找前景的window handle
                    IntPtr foregroundHandle = Win32Message.GetForegroundWindow();
                    // 檢查clientApps內有沒有這個handle
                    ClientApp targetApp = clientApps.FirstOrDefault(app => app.WindowHandle == foregroundHandle);
                    if (targetApp != null)
                    {
                        return targetApp;
                    }
                    else
                        return new ClientApp();
                }
            }
        }

        public AppManager()
        {
            MessageIdentifier = Win32Message.RegisterWindowMessage(WIN_MESSAGE);
            WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "App manager registered message name: " + WIN_MESSAGE);
        }

        public bool AddClientApp(ClientApp targetApp)
        {
            // todo: 如果同樣的product name怎麼辦？
            WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "AddClientApp: " + targetApp.ProductName + ", handle=" + (int)targetApp.WindowHandle);
            return clientApps.Add(targetApp);
        }

        public bool RemoveClientApp(string targetAppName)
        {
            clientApps.RemoveWhere(app => app.ProductName == targetAppName);
            WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "RemoveClientApp: " + targetAppName);
            return true;
        }

        public bool ContainsName(string targetAppName)
        {
            ClientApp targetApp = clientApps.FirstOrDefault(app => app.ProductName == targetAppName);
            if (targetApp != null)
                return true;
            return false;
        }

        /// <summary>
        /// 尋找同名的ClientApp並取代之
        /// </summary>
        /// <param name="targetApp"></param>
        /// <returns></returns>
        public bool UpdateApp(ClientApp targetApp)
        {
            clientApps.RemoveWhere(app => app.ProductName == targetApp.ProductName);
            WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "UpdateApp: " + targetApp.ProductName + ", handle=" + (int)targetApp.WindowHandle);
            return clientApps.Add(targetApp);
        }

        public bool SendMessageToApp(string targetAppName, int wParam, int lParam)
        {
            ClientApp targetApp = clientApps.FirstOrDefault(app => app.ProductName == targetAppName);
            if (targetApp != null)
            {
                WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "PostMessage to app handle=" + (int)targetApp.WindowHandle + ", wParam=" + wParam + ", lParam=" + lParam);
                return Win32Message.PostMessage(targetApp.WindowHandle, MessageIdentifier, wParam, lParam);
            }
            WriteLog(LOG_LEVEL.LL_SUB_FUNC, "SendMessageToApp error: cannot find foreground app");
            return false;
        }

        public int GetCount()
        {
            return clientApps.Count();
        }

        /// <summary>
        /// 如果存檔案之後做了UpdateApp()，那麼這個App就找不到上次的暫存位置了！
        /// </summary>
        public string GetFileNameByApp(string productName)
        {
            if (string.IsNullOrEmpty(productName))
            {
                WriteLog(LOG_LEVEL.LL_SUB_FUNC, "GetFileNameByApp: productName is enpty.");
                return string.Empty;
            }

            ClientApp targetApp = clientApps.FirstOrDefault(app => app.ProductName == productName);
            string fileName = string.Empty;
            if (targetApp != null)
            {
                fileName = targetApp.LastScanFileName;
            }
            return fileName;
        }

        /// <summary>
        /// 儲存暫存檔案至app。下次呼叫UpdateApp()就會被刪掉了。
        /// </summary>
        public bool SetLastFileByApp(string productName, string fileName)
        {
            if (string.IsNullOrEmpty(productName))
            {
                WriteLog(LOG_LEVEL.LL_SUB_FUNC, "SetLastFileByApp: productName is enpty.");
                return false;
            }

            ClientApp targetApp = clientApps.FirstOrDefault(app => app.ProductName == productName);
            if (targetApp != null)
            {
                targetApp.LastScanFileName = fileName;
                return true;
            }
            return false;
        }

        public void RemoveEverything()
        {
            clientApps.Clear();
            connectionNameDict.Clear();
            WriteLog(LOG_LEVEL.LL_SUB_FUNC, "Remove Everything!");
        }

        private void WriteLog(LOG_LEVEL logLevel, string logString)
        {
            string logName = nameof(AppManager);
            Logger.WriteLog(logName, logLevel, logString);
        }
    }

    public class ClientApp
    {
        //public string ConnectionName;
        public string ProductName;
        public string LastScanFileName;
        public IntPtr WindowHandle;
        public Rect TargetWindowRect;
    }
}
