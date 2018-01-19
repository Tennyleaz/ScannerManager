using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Test_app
{
    /// <summary>
    /// App.xaml 的互動邏輯
    /// </summary>
    public partial class App : Application
    {
        private Mutex _instanceMutex = null;

        protected override void OnStartup(StartupEventArgs e)
        {
            // check enough space for log in %APPDATA%
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (Utility.StorageChecker.CheckDiskFreeSpace(path) == false)
            {
                MessageBox.Show("Error:\nNo enough disk space.", "Scanner Manager");
                App.Current.Shutdown();
                Process.GetCurrentProcess().Kill();
                return;
            }

            // check that there is only one instance of the control panel running...
            bool createdNew = true;
            string strAppMutex = "ScannerManagerApp";

            _instanceMutex = new Mutex(true, strAppMutex, out createdNew);

            if (!createdNew)
            {
                _instanceMutex = null;

                App.Current.Shutdown();
                Process.GetCurrentProcess().Kill();
                return;
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_instanceMutex != null)
                _instanceMutex.ReleaseMutex();
            base.OnExit(e);
        }
    }
}
