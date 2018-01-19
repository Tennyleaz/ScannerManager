using System;
using System.Collections.Generic;
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
    /// DrawPreview.xaml 的互動邏輯
    /// 這是在指定的Rectangle上面顯示圖片用的視窗
    /// </summary>
    public partial class DrawPreview : Window
    {
        public DrawPreview(Rect drawBoundary)
        {
            InitializeComponent();
            Loaded += DrawPreview_Loaded;

            this.Top = drawBoundary.Top;
            this.Left = drawBoundary.Left;
            this.Height = drawBoundary.Height;
            this.Width = drawBoundary.Width;
        }

        private void DrawPreview_Loaded(object sender, RoutedEventArgs e)
        {
            WindowInteropHelper wndHelper = new WindowInteropHelper(this);
            int exStyle = (int)Win32Message.GetWindowLong(wndHelper.Handle, (int)Win32Message.GetWindowLongFields.GWL_EXSTYLE);
            exStyle |= (int)Win32Message.ExtendedWindowStyles.WS_EX_TOOLWINDOW;
            SetWindowLong(wndHelper.Handle, (int)Win32Message.GetWindowLongFields.GWL_EXSTYLE, (IntPtr)exStyle);
        }

        internal void UpdateImage(BitmapImage imgSource)
        {
            if (imgSource == null)
                return;
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(
                    new Action(
                        () =>
                        {
                            previewImage.Source = imgSource;
                        })
                    );
            }
            else
            {
                previewImage.Source = imgSource;
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
