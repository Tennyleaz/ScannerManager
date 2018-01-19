using System;
using System.Collections.Generic;
using System.Windows;
using TwainDotNet;
using System.Drawing;
using System.Drawing.Imaging;
using TwainDotNet.TwainNative;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using System.IO;

namespace ScannerManager
{
    /// <summary>
    /// A8ScanWindow.xaml 的互動邏輯
    /// 這是給A8掃描時候抓Handle用的視窗。預設會藏在左上角
    /// </summary>
    public partial class A8ScanWindow : Window
    {
        private Twain _twain;
        private ScanSettings _settings;
        private int imageCount = 0;
        private string _scannerName;
        //private Bitmap resultImage;
        private BitmapImage resultBitmapImage;
        private IList<string> sourceList;
        //private DispatcherTimer timer;
        private Action<int, BitmapImage> myImageCallback;  //用這個callback來傳回圖片給caller
        private int count = 1;

        public delegate void TransferTempImageHandler(object sender, TransferImageEventArgs e);  //定義Event handler
        //public event TransferTempImageHandler TransferTempImage;  //做一份實例
        public event TransferTempImageHandler TransferCompleteImage;  //做一份實例
        public A8Result Result = A8Result.Fail;

        public A8ScanWindow(Action<int, BitmapImage> imageCallback)
        {
            InitializeComponent();
            Loaded += ScanWindow_Loaded;
            Closing += Window_Closing;
            //Closed += ScanWindow_Closed;

            ShowActivated = false;

            Left = -100;
            Top = -100;

            myImageCallback = imageCallback;
        }
        
        public void OnTransferTempImage(Bitmap image, bool continueScanning, float percentage)
        {
            try
            {
                //TransferImageEventArgs e = new TransferImageEventArgs(image, continueScanning, percentage);
                //TransferTempImage?.Invoke(this, e);
                resultBitmapImage = BitmapToImageSource(image);
                image?.Dispose();
                myImageCallback(count, resultBitmapImage);
                count++;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        [Obsolete]
        public void OnTransferComplete(Bitmap image)
        {
            TransferImageEventArgs e = new TransferImageEventArgs(image, false, 1);
            TransferCompleteImage?.Invoke(this, e);
        }

        private Bitmap SwapRedAndBlueChannels(Bitmap bitmap)
        {
            var imageAttr = new ImageAttributes();
            imageAttr.SetColorMatrix(new ColorMatrix(
                                         new[]
                                             {
                                                 new[] {0.0F, 0.0F, 1.0F, 0.0F, 0.0F},
                                                 new[] {0.0F, 1.0F, 0.0F, 0.0F, 0.0F},
                                                 new[] {1.0F, 0.0F, 0.0F, 0.0F, 0.0F},
                                                 new[] {0.0F, 0.0F, 0.0F, 1.0F, 0.0F},
                                                 new[] {0.0F, 0.0F, 0.0F, 0.0F, 1.0F}
                                             }
                                         ));
            var temp = new Bitmap(bitmap.Width, bitmap.Height);
            GraphicsUnit pixel = GraphicsUnit.Pixel;
            using (Graphics g = Graphics.FromImage(temp))
            {
                g.DrawImage(bitmap, System.Drawing.Rectangle.Round(bitmap.GetBounds(ref pixel)), 0, 0, bitmap.Width, bitmap.Height,
                            GraphicsUnit.Pixel, imageAttr);
            }

            return temp;
        }

        private void _twain_TransferImage(object sender, TransferImageEventArgs e)
        {
            //IsEnabled = true;
            if (e.Image != null)
            {
                //resultImage?.Dispose();
                Bitmap resultImage = SwapRedAndBlueChannels(e.Image);
                /*string savePath = @"C:\Users\Tenny\Pictures\TwainTest\testBufferPic_";
                savePath += imageCount.ToString() + @".bmp";
                resultImage.Save(savePath, System.Drawing.Imaging.ImageFormat.Bmp);*/
                OnTransferTempImage(resultImage, e.ContinueScanning, e.PercentComplete);
                //fileName.Content = savePath;

                //IntPtr hbitmap = new Bitmap(e.Image).GetHbitmap();
                //MainImage.Source = Imaging.CreateBitmapSourceFromHBitmap(
                //        hbitmap,
                //        IntPtr.Zero,
                //        Int32Rect.Empty,
                //        BitmapSizeOptions.FromEmptyOptions());
                //Gdi32Native.DeleteObject(hbitmap);

                imageCount++;
            }
        }

        private void ScanWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _twain = new Twain(new WpfWindowMessageHook(this));
                _twain.TransferImage += _twain_TransferImage;
                _twain.ScanningComplete += _twain_ScanningComplete;
                //delegate
                //{
                //    myImageCallback(-1, resultImage);
                //    //OnTransferComplete(resultImage);
                //    //IsEnabled = true;
                //    this.Close();
                //};
                sourceList = _twain.SourceNames;

                /*timer = new DispatcherTimer();
                timer.Tick += Timer_Tick;
                timer.Interval = new TimeSpan(0, 0, 0, 0, 300);
                timer.Start();*/
                
            }
            catch (DeviceOpenExcetion)
            {
                Result = A8Result.ScannerOpenFail;
            }
            catch (TwainException ex)
            {
                Console.WriteLine(ex);
                Utility.Logger.WriteLog("A8Scanner", Utility.LOG_LEVEL.LL_SUB_FUNC, "Twain init exception:" + ex);
                Result = A8Result.OtherTwainException;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Utility.Logger.WriteLog("A8Scanner", Utility.LOG_LEVEL.LL_SERIOUS_ERROR, "Other init exception:" + ex);
                Result = A8Result.Fail;
            }

            // scan有自己的例外處理，這裡就不抓他了
            Scan();
        }

        private void _twain_ScanningComplete(object sender, ScanningCompleteEventArgs e)
        {
            Result = A8Result.Success_NoReturn;
            //resultBitmapImage = BitmapToImageSource(resultImage);
            myImageCallback(-1, resultBitmapImage);
            //OnTransferComplete(resultImage);
            //IsEnabled = true;
            this.Close();
        }

        private void Scan()
        {
            //timer.Stop();

            imageCount = 0;
            _settings = new ScanSettings
            {
                UseDocumentFeeder = true,
                ShowTwainUI = false,
                ShowProgressIndicatorUI = false,
                UseDuplex = false,
                Resolution = ResolutionSettings.ColourPhotocopier,  // 顏色
                /*Area = null,*/
                ShouldTransferAllPages = true,
                Rotation = new RotationSettings
                {
                    AutomaticRotate = false,
                    AutomaticBorderDetection = false  // A8好像不支援這個
                }
            };
            _settings.Page = new PageSettings();  // page參數好像沒什麼用
            _settings.Page.Size = PageType.None;
            //_settings.Page.Orientation = TwainDotNet.TwainNative.Orientation.Auto;
            _settings.Area = new AreaSettings(Units.Inches, 0, 0, 3.7f, 2.1f);  // 設定掃描紙張的邊界
            _settings.AbortWhenNoPaperDetectable = true;  // 要偵測有沒有紙
            _settings.Contrast = 450;

            try
            {
                _scannerName = /*"WIA-A8 ColorScanner PP"; */"A8 ColorScanner PP"; //sourceList.Last()*/;
                _twain.SelectSource(_scannerName);
                _twain.StartScanning(_settings);  // 做open→設定各種CAPABILITY→EnableDS→等回傳圖
                /// Once the Source is enabled via the DG_CONTROL / DAT_USERINTERFACE/ MSG_ENABLEDS operation, 
                /// all events that enter the application’s main event loop must be immediately forwarded to the Source.
                /// The Source, not the application, controls the transition from State 5 to State 6.
                /// - spec pdf p.3-60
            }
            catch (FeederEmptyException)
            {
                //MessageBox.Show("沒有紙");
                Utility.Logger.WriteLog("A8Scanner", Utility.LOG_LEVEL.LL_SUB_FUNC, "Try scan but no paper detected, will close.");
                Result = A8Result.FeederEmpty;
                Close();
            }
            catch (DeviceOpenExcetion)
            {
                Result = A8Result.ScannerOpenFail;
                Utility.Logger.WriteLog("A8Scanner", Utility.LOG_LEVEL.LL_SERIOUS_ERROR, "Twain scan cannot open DS");
                Utility.Logger.WriteLog("A8Scanner", Utility.LOG_LEVEL.LL_SUB_FUNC, "Closing A8 scan window.");
                Close();
            }
            catch (TwainException ex)
            {
                Console.WriteLine(ex);
                Utility.Logger.WriteLog("A8Scanner", Utility.LOG_LEVEL.LL_SERIOUS_ERROR, "Twain scan exception:" + ex);
                Utility.Logger.WriteLog("A8Scanner", Utility.LOG_LEVEL.LL_SUB_FUNC, "Closing A8 scan window.");
                Result = A8Result.OtherTwainException;
                Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Utility.Logger.WriteLog("A8Scanner", Utility.LOG_LEVEL.LL_SERIOUS_ERROR, "Other scan exception:" + ex);
                Utility.Logger.WriteLog("A8Scanner", Utility.LOG_LEVEL.LL_SUB_FUNC, "Closing A8 scan window.");
                Result = A8Result.Fail;
                Close();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _twain?.Dispose();
            Console.WriteLine("A8 Scan Window closing");
        }

        private void ScanWindow_Closed(object sender, EventArgs e)
        {
            Console.WriteLine("A8 Scan Window closed");
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
                bitmapimage.Rotation = Rotation.Rotate270;
                bitmapimage.EndInit();
                bitmap.Dispose();
                bitmapimage.Freeze();  // doing so, this bitmapimage will be readonly, and being thread-safe.
                return bitmapimage;
            }
        }
    }
}
