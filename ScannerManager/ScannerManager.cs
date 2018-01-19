using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management;
using System.Windows.Threading;
using A6;
using System.ComponentModel;
using System.Windows;
using System.Drawing;
using System.Windows.Media.Imaging;
using System.IO;
using System.Timers;
using Utility;
using NamedPipeWrapper;

namespace ScannerManager
{
    public enum ValidScannerType
    {
        A8, 
        A6,
        NotSupported
    }

    public class ScannerManager
    {
        private const string A6_NAME = "A6 Scanner";          // 裝置管理員顯示的名稱
        private const string A8_NAME = "A8 ColorScanner PP";  // 裝置管理員顯示的名稱
        private const int PULLING_TIMER_INTERVAL_MS = 1000;   // timer tick的間隔(毫秒)
        private HashSet<Scanner> availableScanners;
        private Scanner currentScanner;  // 小心，可能會是null
        private Timer m_pullingTimer;
        private A6Manager m_A6manager = null;
        private A8Manager m_A8manager = null;
        private BackgroundWorker scanningWorker;
        private ScannerPreview scannerPreviewWindow;
        private DrawPreview drawWindow;
        private BitmapImage tempImage;
        private bool isInited = false;
        private string targetAppName;  // 紀錄本次掃描的目標handle，掃完之後傳過去用的        
        private object scanLoopLock = new object();  // lock住 掃描/校正/有沒有紙 的函數區域
        private bool isPaperOn;

        /// <summary>
        /// 當正在掃描/校正中，設為true，掃描結束設為false。
        /// </summary>
        public bool IsBusy { get; private set; }  
        public Rect targetWindowRect;  // 紀錄本次掃描的目標視窗Rectangle，給DrawPreview用的
        public delegate void ScannigCompleteHandler(object sender, ScanningCompleteArgs e);  //定義Event handler        
        public event ScannigCompleteHandler ScanningComplete; //做一份實例
        public delegate void PaperOnEventHandler(object sender, PaperOnEventArgs e);
        public event PaperOnEventHandler PaperEvent;  // 如果pulling途中發現進紙/出紙了，通知target app
        public delegate void MachineEventHandler(object senderm, MachineEventArgs e);
        public event MachineEventHandler MachineEvent;  // 機器插拔的事件

        /// <summary>
        /// Initiail dlls, and start timer (if scanner is connected).
        /// Can only init once. Further init will return immediately and do nothing.
        /// </summary>
        /// <param name="startImmediately"></param>
        /// <returns></returns>
        public bool Init(bool startImmediately = true)
        {
            WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "ScannerManager Init()...");
            if (isInited)
                return true;

            // 初始化drivers
            m_A6manager = new A6Manager();
            m_A6manager.RetrieveTempImage += M_A6manager_RetrieveTempImage;

            m_A8manager = new A8Manager();
            m_A8manager.RetrieveTempImage += M_A8Manager_RetrieveTempImage;

            // 更新可用的scanner列表
            availableScanners = CheckAvailableScanners();
            UpdateCurrentScanner();

            // 掃描的動作定義
            scanningWorker = new BackgroundWorker();
            scanningWorker.DoWork += ScanningWorker_DoWork;
            scanningWorker.ProgressChanged += ScanningWorker_ProgressChanged;
            scanningWorker.RunWorkerCompleted += ScanningWorker_RunWorkerCompleted;
            scanningWorker.WorkerReportsProgress = true;
            scanningWorker.WorkerSupportsCancellation = true;

            // pulling timer
            m_pullingTimer = new Timer(PULLING_TIMER_INTERVAL_MS);
            m_pullingTimer.AutoReset = true;
            m_pullingTimer.Elapsed += M_pullingTimer_Elapsed;            
            //m_pullingTimer.Interval = TimeSpan.FromMilliseconds(1000);
            //m_pullingTimer.Tick += M_pullingTimer_Tick;

            // 註冊插拔與電源事件
            RegisterPlugEvents();
            RegisterPowerSessionEvent();

            // 啟動timer
            if (availableScanners.Count > 0 && startImmediately)
                m_pullingTimer.Start();

            isInited = true;

            Console.WriteLine("ScannerManager Init()...done.");
            WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "ScannerManager Init()...done.");
            return true;
        }

        [Obsolete]
        public string GetCurrentScannerName()
        {
            string name = null;
            switch(currentScanner?.ScannerType)
            {
                case ValidScannerType.A6:
                    return A6_NAME;
                case ValidScannerType.A8:
                    return A8_NAME;
            }
            return name;
        }

        /// <summary>
        /// 可能是null!!
        /// </summary>
        public Scanner GetCurrentScanner()
        {
            return currentScanner;
        }

        /// <summary>
        /// 判斷目前連接的掃描器availableScanners，與專案名稱productName的關聯。Init之後才能呼叫。
        /// 目前僅return true
        /// </summary>
        public bool IsMachineConnectedbyProduct(string productName)
        {
            if (availableScanners.Count == 0 || string.IsNullOrEmpty(productName))
                return false;

            // todo: 判斷productName到底對應誰
            return true;
        }

        /// <summary>
        /// Start timer if timer is stopped.
        /// </summary>
        public void Activate()
        {
            if (m_pullingTimer?.Enabled == false)
                m_pullingTimer?.Start();
        }

        /// <summary>
        /// Stop timer.
        /// </summary>
        public void DeActivate()
        {
            m_pullingTimer?.Stop();
            scanningWorker?.CancelAsync();
        }

        public ResultWrapper IsCalibrationNeeded()
        {
            lock (scanLoopLock)
            {
                IsBusy = true;
                ResultWrapper defaultWrapper = new ResultWrapper();
                defaultWrapper.Result = ScannerResult.UnknownError;
                switch (currentScanner?.ScannerType)
                {
                    case ValidScannerType.A6:
                        defaultWrapper = ToAppScannerResult(m_A6manager.IsCalibrationNeeded());
                        break;
                    case ValidScannerType.A8:
                        defaultWrapper = ToAppScannerResult(m_A8manager.IsCalibrationNeeded());
                        break;
                }

                IsBusy = false;
                return defaultWrapper;
            }
        }

        public ResultWrapper IsPaperOn()
        {
            ResultWrapper defaultWrapper = new ResultWrapper();
            defaultWrapper.Result = ScannerResult.UnknownError;
            switch (currentScanner?.ScannerType)
            {
                case ValidScannerType.A6:
                    defaultWrapper = ToAppScannerResult(m_A6manager.IsPaperOn());
                    break;
                case ValidScannerType.A8:
                    defaultWrapper = ToAppScannerResult(m_A8manager.IsPaperOn());
                    break;
            }

            return defaultWrapper;
        }

        /// <summary>
        /// 校正，會block直到完成或是錯誤，不會檢查有沒有紙，需要手動重啟timer
        /// </summary>
        public ResultWrapper Calibrate()
        {
            lock (scanLoopLock)
            {
                IsBusy = true;
                ResultWrapper defaultWrapper = new ResultWrapper();
                defaultWrapper.Result = ScannerResult.UnknownError;
                switch (currentScanner?.ScannerType)
                {
                    case ValidScannerType.A6:
                        defaultWrapper = ToAppScannerResult(m_A6manager.CalibrateScanner());
                        break;
                    case ValidScannerType.A8:
                        defaultWrapper = ToAppScannerResult(m_A8manager.CalibrateScanner());
                        break;
                }

                //Activate();
                IsBusy = false;
                return defaultWrapper;
            }
        }

        /// <summary>
        /// 此timer事件僅每次檢查是否有紙，並通知前景的App。掃描什麼的動作，要由App下達。
        /// </summary>
        private void M_pullingTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            lock (scanLoopLock)
            {
                //Console.WriteLine("M_pullingTimer_Tick...");
                m_pullingTimer.Stop();

                // 確定沒有掃描器，就離開。
                // 等插入掃描器，timer才會啟動
                if (currentScanner == null)
                {
                    Console.WriteLine("no currentScanner");
                    return;
                }

                // 如果沒有目標App(null)，就離開，timer會在下一個人連接時候啟動。
                // 如果得到空白的ClientApp，就離開等下一次timer事件
                ClientApp app = AppManager.TargetApp;
                if (app == null)  // 沒有任何人註冊app
                {
                    Console.WriteLine("null targetAppName, stop timer");
                    targetAppName = null;
                    return;
                }
                else if (string.IsNullOrEmpty(app.ProductName))  // 前景沒有任何註冊過的app視窗，可能在背景了
                {
                    //Console.WriteLine("empty targetAppName, restart timer");
                    targetAppName = null;
                    // 重啟timer
                    m_pullingTimer.Start();

                    // 檢查是不是有不支援的產品，例如

                    return;
                }
                else
                {
                    // note: 這裡應該沒有用，因為下了Scan命令之後才會進來
                    targetAppName = app.ProductName;  // 紀錄本次掃描的目標handle，掃完之後傳過去用的
                    targetWindowRect = app.TargetWindowRect;  // 紀錄本次掃描的目標視窗Rectangle                    
                }

                // 確定有掃描器且有app後，檢查有沒有紙
                ResultWrapper rw = IsPaperOn();
                if (rw.BooleanStaus == true)
                {
                    // 有紙，通知target app，僅通知一次直到下次進紙
                    if (!isPaperOn)
                    {
                        PaperOnEventArgs arg = new PaperOnEventArgs(targetAppName, true);
                        PaperEvent?.Invoke(this, arg);
                        isPaperOn = true;
                    }
                }
                else
                {
                    // 沒有紙，通知target app，僅通知一次直到下次拔出紙
                    if (isPaperOn)
                    {
                        PaperOnEventArgs arg = new PaperOnEventArgs(targetAppName, false);
                        PaperEvent?.Invoke(this, arg);
                        isPaperOn = false;
                    }
                }
                
                m_pullingTimer.Start();
                // 判斷是要問哪個掃描器
                //scanningWorker.RunWorkerAsync(currentScanner);
            }

            // 等到掃描完成，重新啟動timer，
            // 所以這裡不負責重新啟動，留給scanningWorker結束之後做。
        }

        private void M_A8Manager_RetrieveTempImage(object sender, ScanningCompleteArgs e)
        {
            if (e.ResultImage == null)
                return;

            tempImage = e.ResultImage;
            scanningWorker.ReportProgress(0, tempImage);
        }

        private void M_A6manager_RetrieveTempImage(object sender, RetrieveEventArgs e)
        {
            if (e.BitmapImage == null)
                return;

            // 因為我們在DoWork()函數呼叫A6 Scan，所以這個event handler會在背景觸發，要想辦法用ReportProgress()傳圖片給UI。
            tempImage = BitmapToImageSource(e.BitmapImage);
            scanningWorker.ReportProgress(0, tempImage);
        }

        private void ScanningWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "ScanningWorker_RunWorkerCompleted()...");

            bool showIScanProgress = true;
            ScannerResult scnResult = ScannerResult.UnknownError;

            // 判斷有arguments才能給值，不然怕null
            object[] arguments = e.Result as object[];
            if (arguments.Count() == 2)
            {
                showIScanProgress = (bool)arguments[0];
                scnResult = (ScannerResult)arguments[1];
            }

            // 關閉不用的東西
            // 必須到UI Thread做事
            if (Application.Current.Dispatcher.CheckAccess())
            {
                if(showIScanProgress)
                    scannerPreviewWindow?.Close();                
                else
                    drawWindow?.Close();
            }
            else
            {
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    if (showIScanProgress)
                        scannerPreviewWindow?.Close();
                    else
                        drawWindow?.Close();
                }));
            }

            // 回傳完成的圖片，就算是null還是要回傳Result
            //if (tempImage != null)
            {
                ScanningCompleteArgs args = new ScanningCompleteArgs(tempImage);
                args.TargetAppName = targetAppName;  // 一併傳回目標視窗是誰
                args.Result = scnResult;
                ScanningComplete?.Invoke(this, args);
                tempImage = null;
            }
            GC.Collect();

            // 等到掃描完成，重新啟動timer
            //if(!e.Cancelled)
            Activate();
            IsBusy = false;  // 先前在do work設為true
            WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "ScanningWorker_RunWorkerCompleted()...done.");
        }

        private void ScanningWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            try
            {
                // 這些事情必須要在UI Thread下完成，所以才放在ProgressChanged()裡面
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    if (scannerPreviewWindow == null || !IsWindowOpen<ScannerPreview>())
                    {
                        scannerPreviewWindow = new ScannerPreview();
                        scannerPreviewWindow.Show();
                    }
                    BitmapImage imgSource = e.UserState as BitmapImage;
                    if (imgSource != null)
                        scannerPreviewWindow.UpdateImage(imgSource);
                }
                else
                {
                    // 如果發現在背景thread，切回UI thread再顯示視窗
                    Application.Current.Dispatcher.Invoke(new Action(() => {

                        if (scannerPreviewWindow == null || !IsWindowOpen<ScannerPreview>())
                        {
                            scannerPreviewWindow = new ScannerPreview();
                            scannerPreviewWindow.Show();
                        }
                        BitmapImage imgSource = e.UserState as BitmapImage;
                        if (imgSource != null)
                            scannerPreviewWindow.UpdateImage(imgSource);

                    }));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        // 掃描的主要功能在這裡
        private void ScanningWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            lock (scanLoopLock)
            {
                IsBusy = true;  // 到completed才設false
                WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "ScanningWorker_DoWork()...");
                object[] arguments = e.Argument as object[];
                bool showIScanProgress = (bool)arguments[0];
                ValidScannerType scanner = (ValidScannerType)arguments[1]; //e.Argument;
                WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "showIScanProgress is " + showIScanProgress);
                ScannerResult scnResult = ScannerResult.UnknownError;
                try
                {
                    switch (scanner)
                    {
                        case ValidScannerType.A6:
                            // 前面檢查過有沒有紙了
                            // 如果要畫在指定視窗，則另外設置圖片的Event Handler
                            A6Result a6result;
                            if (!showIScanProgress)
                            {
                                // 移除舊的handler
                                m_A6manager.RetrieveTempImage -= M_A6manager_RetrieveTempImage;
                                // 加上我們要的額外handler
                                m_A6manager.RetrieveTempImage += M_A6manager_DrawOnWindow;
                                //掃描，this is a blocking call
                                a6result = m_A6manager.Scan();
                                // 移除此次handler
                                m_A6manager.RetrieveTempImage -= M_A6manager_DrawOnWindow;
                                // 放回舊的handler
                                m_A6manager.RetrieveTempImage += M_A6manager_RetrieveTempImage;
                            }
                            else
                                a6result = m_A6manager.Scan();  // this is a blocking call
                            scnResult = ToAppScannerResult(a6result).Result;
                            break;
                        case ValidScannerType.A8:
                            // A8檢查有沒有紙還是會產生Window，抓Handle用的
                            // 如果要畫在指定視窗，則另外設置圖片的Event Handler
                            A8Result a8result;
                            if (!showIScanProgress)
                            {
                                // 移除舊的handler
                                m_A8manager.RetrieveTempImage -= M_A8Manager_RetrieveTempImage;
                                // 加上我們要的額外handler
                                m_A8manager.RetrieveTempImage += M_A8manager_DrawOnWindow;
                                //掃描，this is a blocking call
                                a8result = m_A8manager.Scan();
                                // 移除此次handler
                                m_A8manager.RetrieveTempImage -= M_A8manager_DrawOnWindow;
                                // 放回舊的handler
                                m_A8manager.RetrieveTempImage += M_A8Manager_RetrieveTempImage;
                            }
                            else
                                a8result = m_A8manager.Scan();  // this is a blocking call
                            scnResult = ToAppScannerResult(a8result).Result;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    WriteLog(LOG_LEVEL.LL_SERIOUS_ERROR, "ScanningWorker_DoWork() exception: " + ex);
                }
                // 回傳結果: 預覽狀態+掃描結果
                arguments = new object[] { showIScanProgress, scnResult };
                e.Result = arguments;

                // 如果中途有人呼叫取消，等到這裡再把Cancel設為true。
                // 這樣Completed()時候就不會再次啟動timer
                if (scanningWorker.CancellationPending)
                    e.Cancel = true;
                WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "ScanningWorker_DoWork()...done.");
            }
            // 等到掃描完成，worker complet event重新啟動timer
        }

        /// <summary>
        /// 掃描一次，通常是app呼叫，IScan不會自己做。這個函數不會block，僅啟動scanningWorker就返回。
        /// </summary>
        /// <param name="drawOnWindow">false代表要在指定的Rect上面掃描</param>
        /// <returns></returns>
        public bool ScanOnce(bool showIScanProgress)
        {
            object[] arguments = new object[] { showIScanProgress, currentScanner.ScannerType };
            scanningWorker.RunWorkerAsync(arguments);

            /*// 判斷是要問哪個掃描器
            switch (currentScanner)
            {
                case ValidScanners.A6:
                    {
                        // 檢查有沒有紙
                        if (m_A6manager.IsPaperOn())
                        {
                            // 如果要畫在指定視窗，則另外設置圖片的Event Handler
                            if (drawOnWindow)
                            {
                                // 移除舊的handler
                                m_A6manager.RetrieveTempImage -= M_A6manager_RetrieveTempImage;
                                // 加上我們要的額外handler
                                m_A6manager.RetrieveTempImage += M_A6manager_DrawOnWindow;
                                //掃描，this is a blocking call
                                m_A6manager.Scan();  
                                // 關閉視窗必須到UI Thread做事
                                Application.Current.Dispatcher.Invoke(new Action(() =>
                                {
                                    drawWindow?.Close();
                                }));
                                // 移除此次handler
                                m_A6manager.RetrieveTempImage -= M_A6manager_DrawOnWindow;
                                // 放回舊的handler
                                m_A6manager.RetrieveTempImage += M_A6manager_RetrieveTempImage;
                            }
                            else
                                m_A6manager.Scan();  // this is a blocking call
                            return true;
                        }
                        else
                            return false;
                    }
                case ValidScanners.A8:
                    break;
            }*/

            return false;
        }

        // 畫在targetWindowRect上面
        private void M_A6manager_DrawOnWindow(object sender, RetrieveEventArgs e)
        {
            if (e.BitmapImage == null)
                return;

            // 這個event handler會在背景觸發，要想辦法傳圖片給UI。
            tempImage = BitmapToImageSource(e.BitmapImage);
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {

                if (drawWindow == null || !IsWindowOpen<DrawPreview>())
                {
                    drawWindow = new DrawPreview(targetWindowRect);
                    drawWindow.Show();
                }
                BitmapImage imgSource = tempImage;
                if (imgSource != null)
                    drawWindow.UpdateImage(imgSource);

            }));
        }

        private void M_A8manager_DrawOnWindow(object sender, ScanningCompleteArgs e)
        {
            if (e.ResultImage == null)
                return;

            tempImage = e.ResultImage;
            // 這個event handler會在背景觸發，要想辦法傳圖片給UI。
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {

                if (drawWindow == null || !IsWindowOpen<DrawPreview>())
                {
                    drawWindow = new DrawPreview(targetWindowRect);
                    drawWindow.Show();
                }
                BitmapImage imgSource = tempImage;
                if (imgSource != null)
                    drawWindow.UpdateImage(imgSource);

            }));
        }

        /// <summary>
        /// 檢查目前插入的掃描器。在初始化的時候會被呼叫。回傳一個hashset，要由available scanners接住。
        /// </summary>        
        private HashSet<Scanner> CheckAvailableScanners()
        {
            HashSet<Scanner> scanners = new HashSet<Scanner>();

            ManagementObjectSearcher searcher = new ManagementObjectSearcher("Select * from Win32_PnPEntity");
            ManagementObjectCollection coll = searcher.Get();

            foreach (ManagementObject info in coll)
            {
                // Check for device name
                string deviceName = Convert.ToString(info["Caption"]);
                string status = Convert.ToString(info["Status"]);
                uint configManagerErrorCode = Convert.ToUInt32(info["ConfigManagerErrorCode"]);
                if (A6_NAME.Equals(deviceName) || A8_NAME.Equals(deviceName))
                {
                    Scanner scanner = new Scanner(deviceName, StringToScannerType(deviceName), configManagerErrorCode);
                    scanners.Add(scanner);
                    WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "CheckAvailableScanners() find scanner: " + deviceName);
                    WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "status=" + status + ", configManagerErrorCode="+ configManagerErrorCode);
                }
            }            

            return scanners;
        }

        /// <summary>
        /// 從 availableScanners 判斷目前要選誰，沒有就設為null。availableScanners 更新之後一定要呼叫這個函數。
        /// </summary>
        private void UpdateCurrentScanner()
        {
            // A8優先
            if (availableScanners.Count > 0)
            {
                currentScanner = availableScanners.FirstOrDefault(x => x.ScannerType == ValidScannerType.A8);
                // 沒有A8再找A6
                if (currentScanner == null)
                    currentScanner = availableScanners.FirstOrDefault(x => x.ScannerType == ValidScannerType.A6);
            }
            else
                currentScanner = null;  //目前沒有scanner
        }

        public void UnInit()
        {
            m_A6manager = null;
            m_A8manager = null;
            m_pullingTimer?.Stop();
            m_pullingTimer?.Dispose();
            UnRegisterPowerSessionEvent();  // 必需移除這個事件，不然怕有memory leak
            WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "Scanner manager has un-inited");
        }

        #region USB device change events

        /// <summary>
        /// 註冊掃描器的插拔事件。也需要先init成功才行
        /// </summary>
        private void RegisterPlugEvents()
        {
            /// https://www.codeproject.com/Articles/46390/WMI-Query-Language-by-Example
            /// https://stackoverflow.com/questions/620144/detecting-usb-drive-insertion-and-removal-using-windows-service-and-c-sharp
            /// https://msdn.microsoft.com/en-us/library/aa394353%28v=vs.85%29.aspx?f=255&MSPPError=-2147217396
            /// Win32_PnPEntity = 隨插即用裝置
            WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "RegisterPlugEvents()...");

            WqlEventQuery insertQuery = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity'");

            ManagementEventWatcher insertWatcher = new ManagementEventWatcher(insertQuery);
            insertWatcher.EventArrived += new EventArrivedEventHandler(DeviceInsertedEvent);
            insertWatcher.Start();

            WqlEventQuery removeQuery = new WqlEventQuery("SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity'");
            ManagementEventWatcher removeWatcher = new ManagementEventWatcher(removeQuery);
            removeWatcher.EventArrived += new EventArrivedEventHandler(DeviceRemovedEvent);
            removeWatcher.Start();

            WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "RegisterPlugEvents()...done.");
        }

        /// <summary>
        /// 裝置插入之後，判斷是不是要加入availableScanners，然後更新currentScanner
        /// </summary>
        private void DeviceInsertedEvent(object sender, EventArrivedEventArgs e)
        {
            ManagementBaseObject instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            if (instance["Name"] != null)
            {
                string name = instance["Name"] as string;
                if (A8_NAME.Equals(name) ||　A6_NAME.Equals(name))
                {
                    WriteLog(LOG_LEVEL.LL_SUB_FUNC, "Scanner is inserted: " + name);

                    // 偵測是否有驅動程式版本
                    ScannerDriverState state = DriverTester.CheckGivenScannerHasDriver(name);

                    string status = instance["Status"] as string;
                    uint configManagerErrorCode = Convert.ToUInt32(instance["ConfigManagerErrorCode"]);
                    Scanner scanner = new Scanner(name, StringToScannerType(name), configManagerErrorCode);
                    availableScanners.Add(scanner);
                    OnMachineEvent(true, name);
                    //if (state == ScannerDriverState.HasDriver)
                    //{
                    //    if (A8_NAME.Equals(name))
                    //    {
                    //        availableScanners.Add(A8_NAME);
                    //        OnMachineEvent(true, A8_NAME);
                    //    }
                    //    else if (A6_NAME.Equals(name))
                    //    {
                    //        availableScanners.Add(A6_NAME);
                    //        OnMachineEvent(true, A6_NAME);
                    //    }
                    //}
                }

                UpdateCurrentScanner();
                // 如果有目標掃描器，就開始pulling thread
                if (currentScanner != null)
                    m_pullingTimer.Start();
            }
        }

        /// <summary>
        /// 裝置拔出後，判斷是不是要從availableScanners剔除，然後更新currentScanner
        /// </summary>
        private void DeviceRemovedEvent(object sender, EventArrivedEventArgs e)
        {
            ManagementBaseObject instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            if (instance["Name"] != null)
            {
                string name = instance["Name"] as string;
                if (A8_NAME.Equals(name) || A6_NAME.Equals(name))
                {
                    WriteLog(LOG_LEVEL.LL_SUB_FUNC, "Scanner is removed: " + name);
                    availableScanners.RemoveWhere(x => x.ScannerType == StringToScannerType(name));
                    OnMachineEvent(false, name);
                }
                UpdateCurrentScanner();
            }
        }

        /// <summary>
        /// 回報USB scanner的插拔事件
        /// </summary>
        private void OnMachineEvent(bool isConnected, string scannerName)
        {
            ClientApp app = AppManager.TargetApp;
            if (app != null && !string.IsNullOrEmpty(app.ProductName))
            {
                // 有app才需要回報掃描器插拔事件
                MachineEventArgs arg = new MachineEventArgs(app.ProductName, scannerName, isConnected);
                MachineEvent?.Invoke(this, arg);
            }
        }

        #endregion

        #region Power and session events

        /// <summary>
        /// 註冊電源的關機/休眠/登出等事件
        /// </summary>
        private void RegisterPowerSessionEvent()
        {
            /// WARNING:
            /// Because this is a static event, you must detach your event handlers when your application is disposed, or memory leaks will result.
            /// Source:
            /// https://msdn.microsoft.com/en-us/library/microsoft.win32.systemevents.powermodechanged.aspx?f=255&MSPPError=-2147217396
            Microsoft.Win32.SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
            Microsoft.Win32.SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
            Microsoft.Win32.SystemEvents.EventsThreadShutdown += SystemEvents_EventsThreadShutdown;
        }

        private void UnRegisterPowerSessionEvent()
        {
            Microsoft.Win32.SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
            Microsoft.Win32.SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
            Microsoft.Win32.SystemEvents.EventsThreadShutdown -= SystemEvents_EventsThreadShutdown;
        }

        private void SystemEvents_SessionSwitch(object sender, Microsoft.Win32.SessionSwitchEventArgs e)
        {
            // 僅處理登出
            switch (e.Reason)
            {
                case Microsoft.Win32.SessionSwitchReason.SessionLogoff:
                    // 即將登出，清除scanners和停止timer
                    // 登出會關閉所有程式，所以不用處理再次登入
                    WriteLog(LOG_LEVEL.LL_SUB_FUNC, "Session logging off.");
                    break;
                case Microsoft.Win32.SessionSwitchReason.SessionLogon:
                    // 登入
                    WriteLog(LOG_LEVEL.LL_SUB_FUNC, "Session log on.");
                    break;
                case Microsoft.Win32.SessionSwitchReason.ConsoleDisconnect:
                    // 使用者鎖定，或是切換至其他人
                    availableScanners.Clear();
                    currentScanner = null;
                    DeActivate();
                    WriteLog(LOG_LEVEL.LL_SUB_FUNC, "Console disconnected, stop timer and clear scanners.");
                    break;
                case Microsoft.Win32.SessionSwitchReason.ConsoleConnect:
                    // 使用者重新登入
                    availableScanners.Clear();
                    availableScanners = CheckAvailableScanners();
                    UpdateCurrentScanner();
                    Activate();
                    WriteLog(LOG_LEVEL.LL_SUB_FUNC, "Console connected, restart scanning timer.");
                    break;
            }
        }

        private void SystemEvents_PowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
        {
            switch (e.Mode)
            {
                case Microsoft.Win32.PowerModes.Resume:
                    {
                        // 恢復之後重新接上掃描器，更新available scanners和current scanner，重新啟動timer
                        availableScanners.Clear();
                        availableScanners = CheckAvailableScanners();
                        UpdateCurrentScanner();
                        Activate();
                        WriteLog(LOG_LEVEL.LL_SUB_FUNC, "System resumed, restart scanning timer.");
                        break;
                    }
                case Microsoft.Win32.PowerModes.Suspend:
                    {
                        // 即將暫停，清除scanners和停止timer
                        availableScanners.Clear();
                        currentScanner = null;
                        DeActivate();
                        WriteLog(LOG_LEVEL.LL_SUB_FUNC, "System will suspend, stop timer and clear scanners.");
                        break;
                    }
                case Microsoft.Win32.PowerModes.StatusChange:
                    break;
            }
        }

        /// <summary>
        /// 避免關機時候沒有解除註冊，怕memory leak
        /// </summary>
        private void SystemEvents_EventsThreadShutdown(object sender, EventArgs e)
        {
            UnRegisterPowerSessionEvent();
        }

        #endregion

        #region Utility functions

        /// <summary>
        /// 檢查是否有指定類型的視窗啟動了
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <returns></returns>
        private bool IsWindowOpen<T>(string name = "") where T : Window
        {
            return string.IsNullOrEmpty(name)
               ? Application.Current.Windows.OfType<T>().Any()
               : Application.Current.Windows.OfType<T>().Any(w => w.Name.Equals(name));
        }

        /// <summary>
        /// 轉換一個XAML適用的BitmapImage，給顯示進度用
        /// </summary>
        /// <param name="bitmap"></param>
        /// <returns></returns>
        private BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();
                bitmap.Dispose();
                bitmapimage.Freeze();  // doing so, this bitmapimage will be readonly, and being thread-safe.
                return bitmapimage;
            }
        }

        private void WriteLog(LOG_LEVEL logLevel, string logString)
        {
            string logName = nameof(ScannerManager);
            Logger.WriteLog(logName, logLevel, logString);
        }

        

        private ValidScannerType StringToScannerType(string type)
        {
            if (A6_NAME.Equals(type))
                return ValidScannerType.A6;
            else if (A8_NAME.Equals(type))
                return ValidScannerType.A8;
            else
                return ValidScannerType.NotSupported;
        }

        #endregion

        #region Manager to app result parsing

        /// <summary>
        /// 將scanner的type與error code轉換成ScannerResult
        /// </summary>
        /// <param name="scanner"></param>
        /// <returns></returns>
        public ScannerResult ToAppScannerResult(Scanner scanner)
        {
            if (scanner == null)
                return ScannerResult.NoSuchScanner;

            if (scanner.ScannerType == ValidScannerType.NotSupported)
                return ScannerResult.NoSuchScanner;

            switch (scanner.ConfigManagerErrorCode)
            {
                case 0:
                    return ScannerResult.Success;
                default:
                    return ScannerResult.CantFindDriver;
            }
        }

        public ResultWrapper ToAppScannerResult(A6Result result)
        {
            ResultWrapper wrapper = new ResultWrapper();
            switch (result)
            {
                case A6Result.Success_True:
                    wrapper.BooleanStaus = true;
                    wrapper.Result = ScannerResult.Success;
                    break;
                case A6Result.Success_False:
                    wrapper.BooleanStaus = false;
                    wrapper.Result = ScannerResult.Success;
                    break;
                case A6Result.Success_NoReturn:
                    wrapper.Result = ScannerResult.Success;
                    break;
                case A6Result.DllNotLoad:
                    // note: 這裡我還不能分辨，A6到底是被占用、驅動沒有裝、還是單純沒插？
                    wrapper.Result = ScannerResult.CantFindDriver;
                    break;
                case A6Result.NeedCalibrate:
                    wrapper.Result = ScannerResult.NeedCalibrate;
                    break;
                case A6Result.Fail:
                default:
                    wrapper.Result = ScannerResult.UnknownError;
                    break;
            }
            return wrapper;
        }

        public ResultWrapper ToAppScannerResult(A8Result a8result)
        {
            ResultWrapper wrapper = new ResultWrapper();
            switch (a8result)
            {
                case A8Result.Success_True:
                    wrapper.BooleanStaus = true;
                    wrapper.Result = ScannerResult.Success;
                    break;
                case A8Result.Success_False:
                    wrapper.BooleanStaus = false;
                    wrapper.Result = ScannerResult.Success;
                    break;
                case A8Result.Success_NoReturn:
                    wrapper.Result = ScannerResult.Success;
                    break;
                case A8Result.DllNotLoad:                    
                    wrapper.Result = ScannerResult.CantFindDriver;
                    break;
                case A8Result.FeederEmpty:
                    wrapper.Result = ScannerResult.NoPaper;
                    break;
                case A8Result.ScannerOpenFail:
                case A8Result.OtherTwainException:                
                    // note: 歸類可能還可再細分
                    wrapper.Result = ScannerResult.ScannerOpenFail;
                    break;                
                case A8Result.Fail:
                default:
                    wrapper.Result = ScannerResult.UnknownError;
                    break;
            }
            return wrapper;
        }
        #endregion
    }

    public struct ResultWrapper
    {
        public bool BooleanStaus;
        public string StringStatus;
        public ScannerResult Result;
    }

    /// <summary>
    /// 兩個Scanner實例的比較，僅檢查ScannerType是否相同！
    /// </summary>
    public class Scanner : IEquatable<Scanner>
    {
        public uint ConfigManagerErrorCode;
        public ValidScannerType ScannerType;

        private string _deviceName;

        public Scanner(string name, ValidScannerType type, uint errorCode)
        {
            _deviceName = name;
            ConfigManagerErrorCode = errorCode;
            ScannerType = type;
        }

        public Scanner()
        {
            ScannerType = ValidScannerType.NotSupported;
            _deviceName = string.Empty;
            ConfigManagerErrorCode = 0;
        }

        public string FriendlyName
        {
            get { return _deviceName; }
        }

        public bool Equals(Scanner other)
        {
            return ScannerType == other.ScannerType;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return _deviceName.GetHashCode() + ScannerType.GetHashCode();
            }
        }
    }

    #region event classes
    public class ScanningCompleteArgs : EventArgs
    {
        /// <summary>
        /// 掃描結果的BitmapImage，只能讀取
        /// </summary>
        public BitmapImage ResultImage;
        /// <summary>
        /// 在ScannerManager傳回圖片給IScanX.exe用的。紀錄目標名稱，存檔用的
        /// </summary>
        public string TargetAppName;
        /// <summary>
        /// 掃描的結果或是例外狀態
        /// </summary>
        public ScannerResult Result;
        public ScanningCompleteArgs(BitmapImage bmp)
        {
            ResultImage = bmp;
        }
    }

    public class PaperOnEventArgs : EventArgs
    {
        public string TargetAppName;
        public bool HasPaper;
        public PaperOnEventArgs(string targetAppName, bool hasPaper)
        {
            TargetAppName = targetAppName;
            HasPaper = hasPaper;
        }
    }

    public class MachineEventArgs : EventArgs
    {
        public string TargetAppName;
        public string ScannerName;
        public bool IsConnected;
        public MachineEventArgs(string targetAppName, string scannerName, bool isConnected)
        {
            TargetAppName = targetAppName;
            ScannerName = scannerName;
            IsConnected = isConnected;
        }
    }
    #endregion
}
