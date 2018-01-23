using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Media.Imaging;
using TwainDotNet;

namespace ScannerManager
{
    public enum A8Result
    {
        /// <summary>
        /// DLL無法載入，原因可能是驅動沒有裝...
        /// </summary>
        DllNotLoad,
        /// <summary>
        /// 無法開啟DS，通常是被其他人占用，來源是OpenSource擲回的DeviceOpenExcetion
        /// </summary>
        ScannerOpenFail,
        /// <summary>
        /// 校正、掃描等動作但是沒有紙
        /// </summary>
        FeederEmpty,
        /// <summary>
        /// 指令成功，沒有回傳值
        /// </summary>
        Success_NoReturn,
        /// <summary>
        /// 指令成功，回傳false
        /// </summary>
        Success_False,
        /// <summary>
        /// 指令成功，回傳true
        /// </summary>
        Success_True,
        /// <summary>
        /// 其他的TwainDotNet問題，例如capability不支援、指定的source找不到、記憶體變零...
        /// </summary>
        OtherTwainException,
        /// <summary>
        /// 其他不明原因，指令失敗
        /// </summary>
        Fail
    }

    class A8Manager
    {
        public delegate void ScannigHandler(object sender, ScanningCompleteArgs e);  //定義Event handler        
        public event ScannigHandler RetrieveTempImage; //做一份實例

        //private ManualResetEvent signalEvent;
        //private BitmapImage tempImage;
        private object _A8Lock = new object();

        /// <summary>
        /// 建立隱藏的視窗，然後建立Twain實例
        /// </summary>
        /// <returns></returns>
        [Obsolete]
        public void Init()
        {
            //signalEvent = new ManualResetEvent(false);
        }

        /// <summary>
        /// 目前沒實際功用
        /// </summary>
        [Obsolete]
        public void UnInit()
        {

        }

        /// <summary>
        /// 成功的話，回傳Success_True或是Success_False代表有沒有紙，失敗的話是其他例外狀況
        /// </summary>
        /// <returns></returns>
        public A8Result IsPaperOn()
        {
            lock (_A8Lock)
            {
                A8Result bReturn = A8Result.Fail;
                Thread thread = new Thread(() =>
                {
                    A8UtilityWindow A8win = new A8UtilityWindow(A8Utitlty.IsPaperOn);
                    A8win.ShowDialog();
                    bReturn = A8win.Result;
                });
                thread.TrySetApartmentState(ApartmentState.STA);
                thread.Start();

                thread.Join();
                return bReturn;
            }
        }

        /// <summary>
        /// 成功的話，回傳Success_True或是Success_False代表需不需要校正，失敗的話是其他例外狀況
        /// </summary>
        /// <returns></returns>
        public A8Result IsCalibrationNeeded()
        {
            lock (_A8Lock)
            {
                A8Result bReturn = A8Result.Fail;
                Thread thread = new Thread(() =>
                {
                    A8UtilityWindow A8win = new A8UtilityWindow(A8Utitlty.IsNeedCalibrate);
                    A8win.ShowDialog();
                    bReturn = A8win.Result;
                });
                thread.TrySetApartmentState(ApartmentState.STA);
                thread.Start();

                thread.Join();
                return bReturn;
            }
        }

        /// <summary>
        /// 成功的話，回傳Success_True或是Success_False代表校正有執行，執行的結果。失敗的話是其他例外狀況。
        /// </summary>
        /// <returns></returns>
        public A8Result CalibrateScanner()
        {
            lock (_A8Lock)
            {
                A8Result bReturn = A8Result.Fail;
                Thread thread = new Thread(() =>
                {
                    A8UtilityWindow A8win = new A8UtilityWindow(A8Utitlty.DoCalibrate);
                    A8win.ShowDialog();
                    bReturn = A8win.Result;
                });
                thread.TrySetApartmentState(ApartmentState.STA);
                thread.Start();

                thread.Join();
                return bReturn;
            }
        }

        /// <summary>
        /// 成功的話，掃完回傳Success_NoReturn。失敗的話是其他例外狀況。
        /// </summary>
        /// <returns></returns>
        public A8Result Scan()
        {
            lock (_A8Lock)
            {
                A8Result bReturn = A8Result.Fail;
                Thread thread = new Thread(() =>
                {
                    A8ScanWindow A8win = new A8ScanWindow(myCallback);
                    A8win.ShowDialog();
                    bReturn = A8win.Result;
                //A8win.TransferTempImage += A8win_TransferTempImage;
                //A8win.TransferCompleteImage += A8win_TransferCompleteImage;              
                });
                thread.TrySetApartmentState(ApartmentState.STA);
                thread.Start();

                thread.Join();
                //signalEvent.WaitOne(); //This thread will block here until the reset event is sent.
                //signalEvent.Reset();
                return bReturn;
            }
        }

        private void myCallback(int i, BitmapImage bmp)
        {
            //Console.WriteLine(i);
            if (bmp != null)
            {
                lock (bmp)
                {
                    try
                    {
                        //string fileName = @"D:\A8\pic" + i.ToString() + ".bmp";
                        //bmp.Save(fileName, System.Drawing.Imaging.ImageFormat.Bmp);
                        //tempImage = BitmapToImageSource(bmp);
                        ScanningCompleteArgs arg = new ScanningCompleteArgs(bmp);
                        RetrieveTempImage?.Invoke(this, arg);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        // 最後一張bmp這裡會出問題，不知道為什麼。不過反正最後一次call和前一次call的圖不會改變...
                    }
                }
            }
        }

        // 用不到
        private void A8win_TransferCompleteImage(object sender, TwainDotNet.TransferImageEventArgs e)
        {
            //if (e.Image != null)
                //tempImage = BitmapToImageSource(e.Image);
            //signalEvent.Set(); // Let Scan() to complete
        }

        // 用不到
        private void A8win_TransferTempImage(object sender, TwainDotNet.TransferImageEventArgs e)
        {
            //if (e.Image != null)
            //{
            //    //tempImage = BitmapToImageSource(e.Image);
            //    ScanningCompleteArgs arg = new ScanningCompleteArgs(tempImage);
            //    RetrieveTempImage?.Invoke(this, arg);
            //}
        }        
    }
}
