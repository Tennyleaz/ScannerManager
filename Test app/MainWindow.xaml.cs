using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ScannerManager;
using System.Drawing;
using System.IO;
using NamedPipeWrapper;
using Utility;

namespace Test_app
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string TEMP_PATH = @"\Temp\ScannerManager\";
        private ScannerManager.ScannerManager scannerManager;
        private AppManager appManager;
        private NamedPipeWrapper.Server<ToIScanMessage, ToAppMessage> server;
        private Dictionary<string, string> connectionNameDict = new Dictionary<string, string>();  // key:product name, value:connection name
        private ScanningBaseWindow scanningBaseWindow = null;
        private bool toShowLogo = true;
        private ScanSideLogo? scanSide;

        public MainWindow()
        {
            InitializeComponent();
            WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "=== Scanner Manager exe has started. ===");
            scannerManager = new ScannerManager.ScannerManager();
            scannerManager.ScanningComplete += MyManager_ScanningComplete;
            scannerManager.PaperEvent += ScannerManager_PaperOnEvent;
            scannerManager.MachineEvent += ScannerManager_MachineEvent;
            appManager = new AppManager();

            LoadNamedPipeServer();
            //RegisterProcessStartEvent();
            DriverTester.ReadScannerDrivers();
            Console.WriteLine("MainWindow() done.");
            WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "Scanner Manager exe init finish.");
        }

        private void SkipButtonClickedCallback()
        {
            ClientApp targetApp = AppManager.TargetApp;
            if (targetApp != null && !string.IsNullOrEmpty(targetApp.ProductName))
            {
                appManager.SendMessageToApp(targetApp.ProductName, (int)SCAN_MSG.SCAN_MSG_TYPE_STATUS, (int)SCAN_STATUS.SCAN_STATUS_SKIP_BACK_BY_USER);
                WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "Skip button is pressed.");
            }
            else
            {
                Console.WriteLine("Skip button is pressed, but can't find target app.");
                WriteLog(LOG_LEVEL.LL_SUB_FUNC, "Skip button is pressed, but can't find target app.");
            }
        }

        private void ShowScanningBaseWindow()
        {
            if (!toShowLogo)
                return;

            if (Dispatcher.CheckAccess())
            {
                if (scanningBaseWindow == null)
                {
                    scanningBaseWindow = new ScanningBaseWindow(SkipButtonClickedCallback);                    
                    scanningBaseWindow.Show();
                }
                else
                {
                    scanningBaseWindow.SetScreenPositiopn();  //避免工作區變化讓它不在右下角
                    scanningBaseWindow.Visibility = Visibility.Visible;
                }
            }
            else
            {
                Dispatcher.Invoke( () => 
                {
                    if (scanningBaseWindow == null)
                    {
                        scanningBaseWindow = new ScanningBaseWindow(SkipButtonClickedCallback);                        
                        scanningBaseWindow.Show();
                    }
                    else
                    {
                        scanningBaseWindow.SetScreenPositiopn();  //避免工作區變化讓它不在右下角
                        scanningBaseWindow.Visibility = Visibility.Visible;
                    }
                } );
            }
            WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "ScanningBaseWindow is shown");
        }

        private void HideScanningBaseWindow()
        {
            if (Dispatcher.CheckAccess())
            {
                if (scanningBaseWindow != null)
                {
                    scanningBaseWindow.Visibility = Visibility.Hidden;
                }
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    if (scanningBaseWindow != null)
                    {
                        scanningBaseWindow.Visibility = Visibility.Hidden;
                    }
                });
            }
            WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "ScanningBaseWindow is hidden");
        }

        private void ScannerManager_MachineEvent(object senderm, MachineEventArgs e)
        {
            if (e.IsConnected)
            {
                appManager.SendMessageToApps(e.TargetAppNames, (int)SCAN_MSG.SCAN_MSG_TYPE_STATUS, (int)SCAN_STATUS.SCAN_STATUS_MACHINE_ON);
            }
            else
            {
                //appManager.SendMessageToApp(e.TargetAppName, (int)SCAN_MSG.SCAN_MSG_TYPE_STATUS, (int)SCAN_STATUS.SCAN_STATUS_MACHINE_OFF);
                //appManager.SendMessageToAllApp((int)SCAN_MSG.SCAN_MSG_TYPE_STATUS, (int)SCAN_STATUS.SCAN_STATUS_MACHINE_OFF);
                appManager.SendMessageToApps(e.TargetAppNames, (int)SCAN_MSG.SCAN_MSG_TYPE_STATUS, (int)SCAN_STATUS.SCAN_STATUS_MACHINE_OFF);
            }
            WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "send scanner insert/unplug event, IsConnected=" + e.IsConnected);
        }

        private void ScannerManager_PaperOnEvent(object sender, PaperOnEventArgs e)
        {
            if (e.HasPaper)
            {
                appManager.SendMessageToApp(e.TargetAppName, (int)SCAN_MSG.SCAN_MSG_TYPE_STATUS, (int)SCAN_STATUS.SCAN_STATUS_PAPER_ON);
            }
            else
            {
                appManager.SendMessageToApp(e.TargetAppName, (int)SCAN_MSG.SCAN_MSG_TYPE_STATUS, (int)SCAN_STATUS.SCAN_STATUS_PAPER_OFF);
            }
            WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "send paper on/off event to " + e.TargetAppName + ": HasPaper=" + e.HasPaper);
        }

        private void MyManager_ScanningComplete(object sender, ScanningCompleteArgs e)
        {
            if (e.Result == ScannerResult.Success)
            {
                // 有圖片且result成功
                if (e.ResultImage != null)
                {
                    BitmapImage bmp = e.ResultImage as BitmapImage;
                    string tempName = e.TargetAppName + DateTime.Now.ToString("HHmmss") + ".jpg";
                    string path = SavePhoto(bmp, tempName);
                    // 檢查檔案存在與否
                    if (File.Exists(path))
                    {
                        appManager.SetLastFileByApp(e.TargetAppName, path);
                        WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "SavePhoto() app name: " + e.TargetAppName + ", path: " + path);
                        appManager.SendMessageToApp(e.TargetAppName, (int)SCAN_MSG.SCAN_MSG_TYPE_SCAN_RTN_VALUE, (int)XSCAN_RTN_VALUE.SCAN_RTN_FINISH_CARD);
                    }
                    else
                    {
                        appManager.SendMessageToApp(e.TargetAppName, (int)SCAN_MSG.SCAN_MSG_TYPE_SCAN_RTN_VALUE, (int)XSCAN_RTN_VALUE.SCAN_RTN_FAIL_TO_SAVE_IMAGE);
                    }
                }
                else
                {
                    // result成功但是沒圖片，目前先傳會SCAN_RTN_INTERNAL_FAIL
                    appManager.SetLastFileByApp(e.TargetAppName, string.Empty);
                    WriteLog(LOG_LEVEL.LL_SERIOUS_ERROR, "Scanning complete, but result image is null!");
                    appManager.SendMessageToApp(e.TargetAppName, (int)SCAN_MSG.SCAN_MSG_TYPE_SCAN_RTN_VALUE, (int)XSCAN_RTN_VALUE.SCAN_RTN_INTERNAL_FAIL);
                }                
            }
            else
            {
                // 沒有圖片或是有失敗的result，傳回其他的XSCAN_RTN_VALUE
                int returnCode = 0;
                switch (e.Result)
                {
                    case ScannerResult.CantFindDriver:
                        returnCode = (int)XSCAN_RTN_VALUE.SCAN_RTN_DRIVER_NOT_INSTALL;
                        break;
                    case ScannerResult.NoSuchScanner:
                        returnCode = (int)XSCAN_RTN_VALUE.SCAN_RTN_SCANNER_NOT_CONNECTED;
                        break;
                    case ScannerResult.NeedCalibrate:
                        returnCode = (int)XSCAN_RTN_VALUE.SCAN_RTN_SCANNER_NEED_CALIBRATE;
                        break;
                    case ScannerResult.NoPaper:
                        returnCode = (int)XSCAN_RTN_VALUE.SCAN_RTN_PAPER_NOT_EXIST;
                        break;
                    case ScannerResult.PaperJam:
                        returnCode = (int)XSCAN_RTN_VALUE.SCAN_RTN_PAPER_JAM;
                        break;
                    default:
                    case ScannerResult.UnknownError:
                    case ScannerResult.ScannerOpenFail:
                        returnCode = (int)XSCAN_RTN_VALUE.SCAN_RTN_INTERNAL_FAIL;
                        break;
                }
                appManager.SendMessageToApp(e.TargetAppName, (int)SCAN_MSG.SCAN_MSG_TYPE_SCAN_RTN_VALUE, returnCode);
            }
            // 將狀態視窗亮綠燈
            scanningBaseWindow?.SetBusy(false);
        }

        /// <summary>
        /// 儲存成功才會回傳完整圖片的path，失敗可能會傳回空字串
        /// </summary>
        private string SavePhoto(BitmapImage objImage, string fileName)
        {
            // https://stackoverflow.com/questions/35804375/how-do-i-save-a-bitmapimage-from-memory-into-a-file-in-wpf-c
            BitmapEncoder encoder = new JpegBitmapEncoder();            
            string path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + TEMP_PATH;
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                string photolocation = path + fileName;   //file name 

                encoder.Frames.Add(BitmapFrame.Create(objImage));

                using (var filestream = new FileStream(photolocation, FileMode.Create))
                    encoder.Save(filestream);
                return photolocation;
            }
            catch (Exception ex)
            {
                WriteLog(LOG_LEVEL.LL_SERIOUS_ERROR, "SavePhoto failed: " + ex);
            }
            return string.Empty;
        }

        #region UI test buttons
        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            scannerManager.Init();
            btnStart.IsEnabled = false;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            scannerManager.DeActivate();
        }

        private void btnActivate_Click(object sender, RoutedEventArgs e)
        {
            scannerManager.Activate();
        }
        #endregion

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //LoadServer();
        }

        /// <summary>
        /// 利用Named Pipe回傳訊息給client
        /// </summary>
        private void PushMessageToApp(string productName, ToAppMessage message)
        {
            string connectionName = connectionNameDict[productName];
            if (!string.IsNullOrEmpty(connectionName))
            {
                server.PushMessage(message, connectionName);
            }
            else
            {
                Console.WriteLine("productName: " + productName + "not found!");
                WriteLog(LOG_LEVEL.LL_SERIOUS_ERROR, "PushMessageToApp() error, productName: " + productName + "not found!");
            }
        }

        private void DisconnectApp(string ProductName)
        {
            try
            {
                //HideScanningBaseWindow();
                appManager.RemoveClientApp(ProductName);
                connectionNameDict.Remove(ProductName);              
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex);
                // 這裡可能因為dict或是hashset已經空了，會有例外。反正不管它。
            }
            if (appManager.GetCount() == 0)
            {
                scannerManager.DeActivate();
                Console.WriteLine("scannerManager deActivate.");
                WriteLog(LOG_LEVEL.LL_SUB_FUNC, "ScannerManager DeActivate, no app is connected.");
            }
        }

        private void OnError(Exception exception)
        {
            Console.WriteLine(exception);
            WriteLog(LOG_LEVEL.LL_SERIOUS_ERROR, "Named pipe exception: " + exception);
        }

        private void OnClientMessage(NamedPipeConnection<ToIScanMessage, ToAppMessage> connection, ToIScanMessage message)
        {
            Console.WriteLine(message.Command.ToString());
            //WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "Revieved command: " + message.Command.ToString());

            switch (message.Command)
            {
                case PipeCommands.Connect:
                    {
                        CONNECT_RESULT connectResult = CONNECT_RESULT.InternalError;
                        try
                        {
                            ClientApp clientApp = new ClientApp();
                            clientApp.ProductName = message.ProductName;
                            clientApp.WindowHandle = new IntPtr(message.OptionalWindowHandle);
                            if (appManager.ContainsName(message.ProductName))
                            {   // 有同名App存在，那麼僅更新
                                try
                                {
                                    connectionNameDict.Remove(message.ProductName);
                                }
                                catch { }
                                connectionNameDict.Add(message.ProductName, connection.Name);
                                appManager.UpdateApp(clientApp);
                                connectResult = CONNECT_RESULT.Scuuess;
                            }
                            else
                            {
                                connectionNameDict.Add(message.ProductName, connection.Name);
                                appManager.AddClientApp(clientApp);
                            }
                            scannerManager.Init();
                            scannerManager.Activate();

                            // 檢查scannerManager init結果
                            connectResult = CONNECT_RESULT.Scuuess;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                        }

                        ToAppMessage appMsg = new ToAppMessage();
                        appMsg.Command = PipeCommands.Connect;
                        appMsg.ConnectResult = connectResult;  // 只有Connect這一步需要回傳connectResult，剩下的頂多要ScannerResult
                        PushMessageToApp(message.ProductName, appMsg);
                        break;
                    }
                case PipeCommands.GetScanedCardFile:
                    {
                        ToAppMessage appMsg = new ToAppMessage();
                        appMsg.Command = PipeCommands.GetScanedCardFile;
                        appMsg.OptionalFilePath = appManager.GetFileNameByApp(message.ProductName);
                        appMsg.Result = ScannerResult.Success;  // 取得檔案目前都不會回傳失敗的code
                        PushMessageToApp(message.ProductName, appMsg);
                        WriteLog(LOG_LEVEL.LL_SUB_FUNC, "GetScanedCardFile: " + appMsg.OptionalFilePath);
                        break;
                    }
                case PipeCommands.GetScannerProp:
                    {
                        ToAppMessage appMsg = new ToAppMessage();
                        appMsg.Command = PipeCommands.GetScannerProp;
                        Scanner scanner = scannerManager.GetCurrentScanner(message.ProductName);
                        //scannerManager.UpdateCurrentScanner(message.ProductName);
                        appMsg.OptionalFilePath = scanner?.FriendlyName;  // 注意current scanner可能是null
                        appMsg.Result = scannerManager.ToAppScannerResult(scanner);
                        PushMessageToApp(message.ProductName, appMsg);
                        // todo: 把短名稱轉換成長名稱
                        break;
                    }
                case PipeCommands.Calibration:
                    {
                        ResultWrapper wrapper = new ResultWrapper();                        
                        // 檢查忙碌中
                        if (!scannerManager.IsBusy)
                        {
                            // 停止timer，做完再啟動
                            scannerManager.DeActivate();
                            // 檢查有沒有紙
                            wrapper = scannerManager.IsPaperOn(message.ProductName);
                            if (wrapper.Result == ScannerResult.Success && wrapper.BooleanStaus == true)
                            {
                                // 校正，不論有沒有需要
                                wrapper = scannerManager.Calibrate(message.ProductName);
                            }
                            scannerManager.Activate();  // 啟動timer
                        }
                        else
                        {
                            // 忙碌中的結果
                            wrapper.Result = ScannerResult.ManagerBusy;
                            WriteLog(LOG_LEVEL.LL_SUB_FUNC, "Calibration: scanner manager is busy.");
                        }
                        // 回傳
                        /*ToAppMessage appMsg = new ToAppMessage();
                        appMsg.Command = PipeCommands.Calibration;
                        appMsg.OptionalStatus = wrapper.BooleanStaus;
                        appMsg.Result = wrapper.Result;
                        PushMessageToApp(message.ProductName, appMsg);
                        // 再用windows message告訴APP結果，以後可能不需要
                        switch (wrapper.Result)
                        {
                            case ScannerResult.Success:
                                appManager.SendMessageToApp(message.ProductName, (int)SCAN_MSG.SCAN_MSG_TYPE_CALIBRATE_RTN, (int)XCALIBRATE_RTN_VALUE.CALIBRATE_RTN_SUCESS);
                                break;
                            case ScannerResult.NoPaper:
                                appManager.SendMessageToApp(message.ProductName, (int)SCAN_MSG.SCAN_MSG_TYPE_CALIBRATE_RTN, (int)XCALIBRATE_RTN_VALUE.CALIBRATE_RTN_NO_PAPER);
                                break;
                            case ScannerResult.PaperJam:
                                appManager.SendMessageToApp(message.ProductName, (int)SCAN_MSG.SCAN_MSG_TYPE_CALIBRATE_RTN, (int)XCALIBRATE_RTN_VALUE.CALIBRATE_RTN_PAPERJAM);
                                break;
                            case ScannerResult.ManagerBusy:
                            case ScannerResult.ScannerOpenFail:
                                appManager.SendMessageToApp(message.ProductName, (int)SCAN_MSG.SCAN_MSG_TYPE_CALIBRATE_RTN, (int)XCALIBRATE_RTN_VALUE.CALIBRATE_RTN_BUSY);
                                break;
                            case ScannerResult.NoSuchScanner:
                            case ScannerResult.CantFindDriver:
                                appManager.SendMessageToApp(message.ProductName, (int)SCAN_MSG.SCAN_MSG_TYPE_CALIBRATE_RTN, (int)XCALIBRATE_RTN_VALUE.CALIBRATE_RTN_DEVICE_NOT_FOUND);
                                break;
                            default:
                            case ScannerResult.UnknownError:
                                appManager.SendMessageToApp(message.ProductName, (int)SCAN_MSG.SCAN_MSG_TYPE_CALIBRATE_RTN, (int)XCALIBRATE_RTN_VALUE.CALIBRATE_RTN_INTERNAL_FAIL);
                                break;
                        }*/
                        // 重啟timer
                        PrepareAndSendToClientMessage(message.Command, wrapper, message.ProductName);                        
                        break;
                    }
                case PipeCommands.IsCalibrationNeed:
                    {
                        /*ToAppMessage appMsg = new ToAppMessage();
                        appMsg.Command = PipeCommands.IsCalibrationNeed;
                        ResultWrapper wrapper = scannerManager.IsCalibrationNeeded();
                        appMsg.OptionalStatus = wrapper.BooleanStaus;
                        appMsg.Result = wrapper.Result;
                        PushMessageToApp(message.ProductName, appMsg);*/
                        scannerManager.DeActivate();
                        ResultWrapper wrapper = scannerManager.IsCalibrationNeeded(message.ProductName);
                        PrepareAndSendToClientMessage(message.Command, wrapper, message.ProductName);
                        scannerManager.Activate();
                        break;
                    }
                case PipeCommands.IsMachineConnectedbyProduct:
                    {
                        ToAppMessage appMsg = new ToAppMessage();
                        appMsg.Command = PipeCommands.IsMachineConnectedbyProduct;
                        appMsg.OptionalStatus = scannerManager.IsMachineConnectedbyProduct(message.ProductName);
                        appMsg.Result = ScannerResult.Success;
                        PushMessageToApp(message.ProductName, appMsg);
                        break;
                    }                
                case PipeCommands.IsPaperOn:
                    {
                        /*ToAppMessage appMsg = new ToAppMessage();
                        appMsg.Command = PipeCommands.IsPaperOn;
                        ResultWrapper wrapper = scannerManager.IsPaperOn();
                        appMsg.OptionalStatus = wrapper.BooleanStaus;
                        appMsg.Result = wrapper.Result;
                        PushMessageToApp(message.ProductName, appMsg);*/
                        scannerManager.DeActivate();
                        ResultWrapper wrapper = scannerManager.IsPaperOn(message.ProductName);
                        PrepareAndSendToClientMessage(message.Command, wrapper, message.ProductName);
                        scannerManager.Activate();
                        break;
                    }
                // 以下的指令都沒有回傳
                case PipeCommands.ScanCard:
                    {
                        // 檢查忙碌中
                        if (!scannerManager.IsBusy)
                        {
                            // 停止timer，在掃描的completed event會再啟動timer
                            scannerManager.DeActivate();
                            // 決定logo的顯示
                            toShowLogo = message.OptionalStatus;
                            // 將狀態視窗亮紅燈，等到ScanningComplete事件才會轉綠燈
                            ShowScanningBaseWindow();
                            scanningBaseWindow?.SetBusy(true);
                            // 掃描一次
                            scannerManager.ScanOnce(message.ProductName, message.OptionalStatus);
                        }
                        else
                        {
                            // 忙碌中的結果
                            appManager.SendMessageToApp(message.ProductName, (int)SCAN_MSG.SCAN_MSG_TYPE_SCAN_RTN_VALUE, (int)XSCAN_RTN_VALUE.SCAN_RTN_BUSY);
                            WriteLog(LOG_LEVEL.LL_SUB_FUNC, "ScanCard: scanner manager is busy.");
                        }
                        break;
                    }
                case PipeCommands.SetCaller:
                    {
                        ClientApp clientApp = new ClientApp();
                        clientApp.ProductName = message.ProductName;
                        clientApp.WindowHandle = new IntPtr(message.OptionalWindowHandle);                        
                        appManager.UpdateApp(clientApp);
                        break;
                    }
                case PipeCommands.SetSecondaryCaller:
                    {
                        if (message.OptionalWindowHandle != 0)
                        {
                            ClientApp clientApp = new ClientApp();
                            clientApp.ProductName = message.ProductName;
                            clientApp.WindowHandle = new IntPtr(message.OptionalWindowHandle);
                            appManager.UpdateApp(clientApp);
                        }
                        break;
                    }
                case PipeCommands.SetCapture:
                    {
                        WriteLog(LOG_LEVEL.LL_SUB_FUNC, "App " + message.ProductName + " send SetCapture, do nothing.");
                        break;
                    }
                case PipeCommands.SetScanDrawObject:
                    {
                        ClientApp clientApp = new ClientApp();
                        clientApp.ProductName = message.ProductName;
                        clientApp.WindowHandle = new IntPtr(message.OptionalWindowHandle);
                        clientApp.TargetWindowRect = Win32Message.CalculateClientRect(new IntPtr(message.OptionalWindowHandle), message.OptionalScreenRect);
                        appManager.UpdateApp(clientApp);
                        scannerManager.targetWindowRect = clientApp.TargetWindowRect; // 因為怕下一次timer迴圈沒進去就呼叫ScanOnce()，他就沒改變
                        break;
                    }                
                case PipeCommands.ShowSideLogo:
                    {
                        // WC8傳來的scanSide目前只會有front、back、或BACK_CAN_CANCE三種
                        // 傳來0代表不顯示
                        if (Enum.IsDefined(typeof(ScanSideLogo), message.OptionalCardSide))
                            scanSide = (ScanSideLogo)message.OptionalCardSide;
                        else
                            scanSide = null;
                        toShowLogo = message.OptionalStatus;
                        Console.WriteLine("toShowLogo=" + toShowLogo + ", scanSide=" + scanSide);                        
                        if (toShowLogo)
                        {
                            ShowScanningBaseWindow();
                        }
                        else
                        {
                            HideScanningBaseWindow();
                        }
                        // 先顯示log再決定skip按鈕的顯示與否，不然logo出來了按鈕卻沒顯示
                        scanningBaseWindow?.ShowSkipButton(scanSide);
                        Console.WriteLine("ShowSideLogo done.");
                        WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "ShowSideLogo toShowLogo = " + toShowLogo + ", scanSide = " + scanSide);
                        break;
                    }
                case PipeCommands.Disconnect:
                    {
                        DisconnectApp(message.ProductName);
                        break;
                    }
            }

        }

        /// <summary>
        /// 根據command送回ToAppMessage，並且傳送額外的Windows massage。
        /// 掃描器或是AppManager的處理不會在這裡！
        /// 目前處理Calibration/IsCalibrationNeed/IsPaperOn
        /// </summary>
        private void PrepareAndSendToClientMessage(PipeCommands command, ResultWrapper wrapper, string productName)
        {
            // 準備massage
            ToAppMessage appMsg = new ToAppMessage();
            appMsg.Command = command;
            // 根據command類型處理wrapper
            switch (command)
            {
                case PipeCommands.Calibration:
                    {
                        appMsg.OptionalStatus = wrapper.BooleanStaus;
                        appMsg.Result = wrapper.Result;
                        PushMessageToApp(productName, appMsg);
                        // 再用windows message告訴APP結果，以後可能不需要
                        switch (wrapper.Result)
                        {
                            case ScannerResult.Success:
                                appManager.SendMessageToApp(productName, (int)SCAN_MSG.SCAN_MSG_TYPE_CALIBRATE_RTN, (int)XCALIBRATE_RTN_VALUE.CALIBRATE_RTN_SUCESS);
                                break;
                            case ScannerResult.NoPaper:
                                appManager.SendMessageToApp(productName, (int)SCAN_MSG.SCAN_MSG_TYPE_CALIBRATE_RTN, (int)XCALIBRATE_RTN_VALUE.CALIBRATE_RTN_NO_PAPER);
                                break;
                            case ScannerResult.PaperJam:
                                appManager.SendMessageToApp(productName, (int)SCAN_MSG.SCAN_MSG_TYPE_CALIBRATE_RTN, (int)XCALIBRATE_RTN_VALUE.CALIBRATE_RTN_PAPERJAM);
                                break;
                            case ScannerResult.ManagerBusy:
                            case ScannerResult.ScannerOpenFail:
                                appManager.SendMessageToApp(productName, (int)SCAN_MSG.SCAN_MSG_TYPE_CALIBRATE_RTN, (int)XCALIBRATE_RTN_VALUE.CALIBRATE_RTN_BUSY);
                                break;
                            case ScannerResult.NoSuchScanner:
                            case ScannerResult.CantFindDriver:
                                appManager.SendMessageToApp(productName, (int)SCAN_MSG.SCAN_MSG_TYPE_CALIBRATE_RTN, (int)XCALIBRATE_RTN_VALUE.CALIBRATE_RTN_DEVICE_NOT_FOUND);
                                break;
                            default:
                            case ScannerResult.UnknownError:
                                appManager.SendMessageToApp(productName, (int)SCAN_MSG.SCAN_MSG_TYPE_CALIBRATE_RTN, (int)XCALIBRATE_RTN_VALUE.CALIBRATE_RTN_INTERNAL_FAIL);
                                break;
                        }
                        break;
                    }
                case PipeCommands.IsCalibrationNeed:
                case PipeCommands.IsPaperOn:
                    {
                        appMsg.OptionalStatus = wrapper.BooleanStaus;
                        appMsg.Result = wrapper.Result;
                        PushMessageToApp(productName, appMsg);
                        break;
                    }

            }
        }

        private void OnClientDisconnected(NamedPipeConnection<ToIScanMessage, ToAppMessage> connection)
        {
            Console.WriteLine("Client id " + connection.Id + " is disconnected.");
            string disconnectName = connectionNameDict.FirstOrDefault(x => x.Value == connection.Name).Key;
            DisconnectApp(disconnectName);
            HideScanningBaseWindow();
            WriteLog(LOG_LEVEL.LL_SUB_FUNC, "Client id " + connection.Id + " is disconnected from named pipe server.");
        }

        private void OnClientConnected(NamedPipeConnection<ToIScanMessage, ToAppMessage> connection)
        {
            Console.WriteLine("Client id " + connection.Id + " is connected.");
            WriteLog(LOG_LEVEL.LL_SUB_FUNC, "Client id " + connection.Id + " " + connection.Name + " is connected to named pipe server.");
            // named pipe連上之後我還不能知道他是哪個APP，必須等到App發送PipeCommands.Connect給我才行。
        }

        private void WriteLog(LOG_LEVEL logLevel, string logString)
        {
            string logName = "ScannerManagerApp";
            Logger.WriteLog(logName, logLevel, logString);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            server.Stop();
            scannerManager.DeActivate();
            scannerManager.UnInit();
            appManager.RemoveEverything();
            scanningBaseWindow?.ForceClose();
            ClearTempFolder();
            WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "Scanner Manager exe is closing.");
        }

        private void LoadNamedPipeServer()
        {
            // 啟動server，因為沒有視窗的話Window loaded事件不會觸發，就放到Constructor做。
            // https://stackoverflow.com/questions/6690848/completely-hide-wpf-window-on-startup
            WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "LoadServer()...");
            server = new Server<ToIScanMessage, ToAppMessage>("Tenny", null);
            server.ClientConnected += OnClientConnected;
            server.ClientDisconnected += OnClientDisconnected;
            server.ClientMessage += OnClientMessage;
            server.Error += OnError;
            server.Start();
            WriteLog(LOG_LEVEL.LL_NORMAL_LOG, "LoadServer()...done.");
        }

        private void ClearTempFolder()
        {
            // 清理資料夾/檔案
            string path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + TEMP_PATH;            
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void RegisterProcessStartEvent()
        {
            /// REF:
            /// https://www.fluxbytes.com/csharp/how-to-know-if-a-process-exited-or-started-using-events-in-c/
            /// https://stackoverflow.com/questions/4908906/c-sharp-raise-an-event-when-a-new-process-starts
            /// https://msdn.microsoft.com/en-us/library/system.management.wqleventquery.withininterval%28v=vs.110%29.aspx?f=255&MSPPError=-2147217396
            /// 
            /// Win32_ProcessStartTrace need admin permission, but __InstanceCreationEvent don't need.
            /// WITHIN 2: pulling for each 2 seconds.
            try
            {
                WqlEventQuery query = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_Process'");
                ManagementEventWatcher startWatch = new ManagementEventWatcher(query);
                startWatch.EventArrived += StartWatch_EventArrived;
                startWatch.Start();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }

        private void StartWatch_EventArrived(object sender, EventArrivedEventArgs e)
        {
            string testName = "notepad.exe";
            string targetName = "WorldCardTeam.exe";
            try
            {
                ManagementBaseObject instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
                if (instance != null)
                {
                    string name = instance["Name"] as string;
                    if (targetName.Equals(name) || testName.Equals(name))
                    {
                        MessageBox.Show(name + " found!");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
