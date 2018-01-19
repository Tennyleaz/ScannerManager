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
            double x = System.Windows.SystemParameters.WorkArea.Width;
            double y = System.Windows.SystemParameters.WorkArea.Height;
            // ScanningBaseWindow的高度目前是40px
            int baseWindowHeight = 40;
            this.Left = x - this.Width;
            this.Top = y - this.Height - baseWindowHeight;
        }

        public void UpdateImage(Bitmap bmp)
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
        }

        public void UpdateImage(BitmapImage bmp)
        {
            if (bmp == null)
                return;
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(
                    new Action(
                        () =>
                        {
                            previewImage.Source = bmp;
                        })
                    );
            }
            else
            {
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
