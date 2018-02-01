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
                    ClientApp targetApp = clientApps.FirstOrDefault(app => 
                        app.WindowHandle == foregroundHandle 
                        || (app.ChildWindowHandle == foregroundHandle && app.ChildWindowHandle != IntPtr.Zero));

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
            ProductFilter.GetProducts().AddApp(targetApp.ProductName);
            return clientApps.Add(targetApp);
        }

        public bool RemoveClientApp(string targetAppName)
        {
            if (!string.IsNullOrEmpty(targetAppName))
            {
                clientApps.RemoveWhere(app => app.ProductName == targetAppName);
                ProductFilter.GetProducts().RemoveApp(targetAppName);
                WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "RemoveClientApp: " + targetAppName);
            }
            else
            {
                WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "RemoveClientApp: no target app name!");
                return false;
            }
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
        /// 尋找同名的ClientApp並更新rect, 兩個handle
        /// </summary>
        /// <param name="targetApp"></param>
        /// <returns></returns>
        public bool UpdateApp(ClientApp inApp)
        {
            //clientApps.RemoveWhere(app => app.ProductName == targetApp.ProductName);
            //WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "UpdateApp: " + targetApp.ProductName + ", handle=" + (int)targetApp.WindowHandle);
            //return clientApps.Add(targetApp);
            ClientApp targetApp = clientApps.FirstOrDefault(app => app.ProductName == inApp.ProductName);
            if (targetApp != null)
            {
                targetApp.TargetWindowRect = inApp.TargetWindowRect;
                if (inApp.WindowHandle != IntPtr.Zero)
                    targetApp.WindowHandle = inApp.WindowHandle;
                targetApp.ChildWindowHandle = inApp.ChildWindowHandle;
                return true;
            }
            else
            {
                return clientApps.Add(inApp);
            }
        }

        public bool UpdateAppHandle(string targetAppName, IntPtr handle)
        {
            if (string.IsNullOrEmpty(targetAppName))
            {
                WriteLog(LOG_LEVEL.LL_SUB_FUNC, "UpdateAppChildHandle: productName is enpty.");
                return false;
            }

            ClientApp targetApp = clientApps.FirstOrDefault(app => app.ProductName == targetAppName);
            if (targetApp != null)
            {
                targetApp.WindowHandle = handle;
                return true;
            }
            return false;
        }

        public bool UpdateAppChildHandle(string targetAppName, IntPtr handle)
        {
            if (string.IsNullOrEmpty(targetAppName))
            {
                WriteLog(LOG_LEVEL.LL_SUB_FUNC, "UpdateAppChildHandle: productName is enpty.");
                return false;
            }

            ClientApp targetApp = clientApps.FirstOrDefault(app => app.ProductName == targetAppName);
            if (targetApp != null)
            {
                targetApp.ChildWindowHandle = handle;
                return true;
            }
            return false;
        }

        public bool ClearAppChildHandle(string targetAppName)
        {
            if (string.IsNullOrEmpty(targetAppName))
            {
                WriteLog(LOG_LEVEL.LL_SUB_FUNC, "ClearAppChildHandle: productName is enpty.");
                return false;
            }

            ClientApp targetApp = clientApps.FirstOrDefault(app => app.ProductName == targetAppName);
            if (targetApp != null)
            {
                targetApp.ChildWindowHandle = IntPtr.Zero;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 如果App有Child handle，會優先送給child
        /// </summary>
        /// <param name="targetAppName"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <returns></returns>
        public bool SendMessageToApp(string targetAppName, int wParam, int lParam)
        {
            ClientApp targetApp = clientApps.FirstOrDefault(app => app.ProductName == targetAppName);
            if (targetApp != null)
            {
                if (targetApp.ChildWindowHandle != IntPtr.Zero)
                {
                    WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "PostMessage to app child handle=" + (int)targetApp.ChildWindowHandle + ", wParam=" + wParam + ", lParam=" + lParam);
                    Win32Message.PostMessage(targetApp.ChildWindowHandle, MessageIdentifier, wParam, lParam);
                }
                else
                {
                    WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "PostMessage to app handle=" + (int)targetApp.WindowHandle + ", wParam=" + wParam + ", lParam=" + lParam);
                    return Win32Message.PostMessage(targetApp.WindowHandle, MessageIdentifier, wParam, lParam);
                }
            }
            WriteLog(LOG_LEVEL.LL_SUB_FUNC, "SendMessageToApp error: cannot find foreground app");
            return false;
        }

        /// <summary>
        /// 給機器狀態變化專用的
        /// </summary>
        /// <param name="targetAppNames"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        public void SendMessageToApps(HashSet<string> targetAppNames, int wParam, int lParam)
        {
            if (targetAppNames == null)
                return;
            foreach (string appName in targetAppNames)
            {
                ClientApp targetApp = clientApps.FirstOrDefault(app => app.ProductName == appName);
                if (targetApp != null)
                {
                    WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "PostMessage to app handle=" + (int)targetApp.WindowHandle + ", wParam=" + wParam + ", lParam=" + lParam);
                    Win32Message.PostMessage(targetApp.WindowHandle, MessageIdentifier, wParam, lParam);
                    if (targetApp.ChildWindowHandle != IntPtr.Zero)
                    {
                        WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "PostMessage to app child handle=" + (int)targetApp.ChildWindowHandle + ", wParam=" + wParam + ", lParam=" + lParam);
                        Win32Message.PostMessage(targetApp.ChildWindowHandle, MessageIdentifier, wParam, lParam);
                    }
                }
            }            
        }

        public void SendMessageToAllApps(int wParam, int lParam)
        {
            foreach(var targetApp in clientApps)
            {
                Win32Message.PostMessage(targetApp.WindowHandle, MessageIdentifier, wParam, lParam);
            }
            WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "PostMessage to all app, wParam=" + wParam + ", lParam=" + lParam);
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

        /// <summary>
        /// 取得目標App的window handle
        /// </summary>
        /// <param name="productName"></param>
        /// <returns></returns>
        public IntPtr GetHandleByName(string productName)
        {
            if (string.IsNullOrEmpty(productName))
            {
                WriteLog(LOG_LEVEL.LL_SUB_FUNC, "GetHandleByName: productName is enpty.");
                return IntPtr.Zero;
            }

            ClientApp targetApp = clientApps.FirstOrDefault(app => app.ProductName == productName);
            if (targetApp != null)
            {
                if (targetApp.ChildWindowHandle != IntPtr.Zero)
                    return targetApp.ChildWindowHandle;
                return targetApp.WindowHandle;
            }
            return IntPtr.Zero;
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
        public IntPtr WindowHandle = IntPtr.Zero;
        public IntPtr ChildWindowHandle = IntPtr.Zero;
        public Rect TargetWindowRect;
    }
}
