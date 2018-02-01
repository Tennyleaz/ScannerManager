using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ScannerManager
{
    /// <summary>
    /// ScannerPreview.xaml 的互動邏輯
    /// 這是掃描時候，顯示預覽圖的視窗，放在工作區右下角。
    /// </summary>
    public partial class ScannerPreview : Window
    {
        private double screenX, screenY;
        private const int maxPreviewWidthLandscape = 240;  // 允許預覽圖的最大寬度
        private const int maxPreviewWidthHorizontal = 386;  // 允許預覽圖的最大寬度
        private const int baseWindowHeight = 36;  // ScanningBaseWindow的高度目前是36px
        private bool _lastIsLandscape;

        public ScannerPreview()
        {
            InitializeComponent();
            this.Loaded += ScannerPreview_Loaded;
            SetScreenPositiopn();
        }

        private void ScannerPreview_Loaded(object sender, RoutedEventArgs e)
        {
            WindowInteropHelper wndHelper = new WindowInteropHelper(this);
            int exStyle = (int)Win32Message.GetWindowLong(wndHelper.Handle, (int)Win32Message.GetWindowLongFields.GWL_EXSTYLE);
            exStyle |= (int)Win32Message.ExtendedWindowStyles.WS_EX_TOOLWINDOW;
            SetWindowLong(wndHelper.Handle, (int)Win32Message.GetWindowLongFields.GWL_EXSTYLE, (IntPtr)exStyle);
        }

        /// <summary>
        /// 調整視窗到工作區的右下角，ScanningBaseWindow的上面
        /// </summary>
        private void SetScreenPositiopn()
        {
            screenX = System.Windows.SystemParameters.WorkArea.Width;
            screenY = System.Windows.SystemParameters.WorkArea.Height;
            
            this.Left = screenX - this.Width;
            this.Top = screenY - this.Height - baseWindowHeight;
        }

        private void AdjustWindowSize(double w, double h, bool isLandscape)
        {
            double ratio;
            if (isLandscape)
            {
                ratio = w / maxPreviewWidthHorizontal;
                this.Width = maxPreviewWidthHorizontal;
            }
            else
            {
                ratio = w / maxPreviewWidthLandscape;
                this.Width = maxPreviewWidthLandscape;
            }
            //this.Width = w / ratio;
            this.Height = h / ratio;
            SetScreenPositiopn();
        }

        /*public void UpdateImage(Bitmap bmp)
        {
            if (bmp == null)
                return;
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(
                    new Action(
                        () =>
                        {
                            previewImage.Source = BitmapToImageSource(bmp);                            
                        })
                    );
            }
            else
            {
                previewImage.Source = BitmapToImageSource(bmp);
            }
        }*/

        public void UpdateImage(BitmapImage bmp, bool isLandscape)
        {
            if (bmp == null)
                return;
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(
                    new Action(
                        () =>
                        {
                            AdjustWindowSize(bmp.Width, bmp.Height, isLandscape);
                            previewImage.Source = bmp;
                        })
                    );
            }
            else
            {
                AdjustWindowSize(bmp.Width, bmp.Height, isLandscape);
                previewImage.Source = bmp;
            }
        }

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
                return bitmapimage;
            }
        }

        #region Window styles
        // from:
        // https://stackoverflow.com/questions/357076/best-way-to-hide-a-window-from-the-alt-tab-program-switcher

        public static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            int error = 0;
            IntPtr result = IntPtr.Zero;
            // Win32 SetWindowLong doesn't clear error on success
            Win32Message.SetLastError(0);

            if (IntPtr.Size == 4)
            {
                // use SetWindowLong
                Int32 tempResult = Win32Message.IntSetWindowLong(hWnd, nIndex, IntPtrToInt32(dwNewLong));
                error = Marshal.GetLastWin32Error();
                result = new IntPtr(tempResult);
            }
            else
            {
                // use SetWindowLongPtr
                result = Win32Message.IntSetWindowLongPtr(hWnd, nIndex, dwNewLong);
                error = Marshal.GetLastWin32Error();
            }

            if ((result == IntPtr.Zero) && (error != 0))
            {
                throw new System.ComponentModel.Win32Exception(error);
            }

            return result;
        }

        private static int IntPtrToInt32(IntPtr intPtr)
        {
            return unchecked((int)intPtr.ToInt64());
        }
        #endregion
    }
}
