using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using ScannerManager;

namespace Test_app
{
    /// <summary>
    /// ScanningBaseWindow.xaml 的互動邏輯
    /// </summary>
    public partial class ScanningBaseWindow : Window
    {
        private bool _isBusy;
        private Action myCallbackFunction;  // skip按下的callback
        private bool allowClose = false;

        public bool IsBusy
        {
            get { return _isBusy; }
            private set
            {
                _isBusy = value;
                if (_isBusy)
                    rectStatus.Fill = Brushes.Red;
                else
                    rectStatus.Fill = Brushes.Green;
            }
        }

        public ScanningBaseWindow(Action skipButtonCallback)
        {
            InitializeComponent();
            myCallbackFunction = skipButtonCallback;
            SetScreenPositiopn();
        }

        private void btnSkip_Click(object sender, RoutedEventArgs e)
        {
            myCallbackFunction();  //通知MainWindow
            btnSkip.IsEnabled = false;
            lbFront.Visibility = Visibility.Visible;
            lbBack.Visibility = Visibility.Hidden;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!allowClose)
            {
                // 不要關掉它，隱藏就好
                e.Cancel = true;
                this.Visibility = Visibility.Hidden;
            }
        }

        /// <summary>
        /// 調整視窗到工作區的右下角，每次叫出視窗都要呼叫一次，避免螢幕改變而視窗沒動
        /// </summary>
        public void SetScreenPositiopn()
        {            
            double x = System.Windows.SystemParameters.WorkArea.Width;
            double y = System.Windows.SystemParameters.WorkArea.Height;
            this.Left = x - this.Width;
            this.Top = y - this.Height;
        }

        /// <summary>
        /// 設定燈號和按鈕的狀態
        /// </summary>
        /// <param name="isBusy"></param>
        public void SetBusy(bool isBusy)
        {
            if (Dispatcher.CheckAccess())
            {
                IsBusy = isBusy;
                btnSkip.IsEnabled = !isBusy;
            }
            else
            {
                Dispatcher.Invoke( () =>
                {
                    IsBusy = isBusy;
                    btnSkip.IsEnabled = !isBusy;
                });
            }
        }

        /// <summary>
        /// 顯示/隱藏skip按鈕和back標誌
        /// </summary>
        /// <param name="isVisible"></param>
        public void ShowSkipButton(Utility.ScanSideLogo? scanSide)
        {
            // WC8傳來的scanSide目前只會有front、back、或BACK_CAN_CANCE三種
            // BACK_CAN_CANCEL和back的話再顯示back字樣
            // BACK_CAN_CANCE再顯示取消按鈕
            bool isButoonVisible = false;
            if (scanSide.HasValue && scanSide == Utility.ScanSideLogo.SSL_BACK_CAN_CANCEL)
                isButoonVisible = true;

            bool isBackSideVisible = false;
            if (scanSide.HasValue && (scanSide == Utility.ScanSideLogo.SSL_BACK || scanSide == Utility.ScanSideLogo.SSL_BACK_CAN_CANCEL))
                isBackSideVisible = true;

            if (Dispatcher.CheckAccess())
            {
                btnSkip.Visibility = isButoonVisible ? Visibility.Visible : Visibility.Hidden;
                lbBack.Visibility = isBackSideVisible ? Visibility.Visible : Visibility.Hidden;
                lbFront.Visibility = isBackSideVisible ? Visibility.Hidden : Visibility.Visible;
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    btnSkip.Visibility = isButoonVisible ? Visibility.Visible : Visibility.Hidden;
                    lbBack.Visibility = isBackSideVisible ? Visibility.Visible : Visibility.Hidden;
                    lbFront.Visibility = isBackSideVisible ? Visibility.Hidden : Visibility.Visible;
                });
            }
        }

        /// <summary>
        /// 將allowClose設為true，所以Closing事件就會繼續執行
        /// </summary>
        public void ForceClose()
        {
            allowClose = true;
            Close();
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            //Set the window style to noactivate.
            WindowInteropHelper helper = new WindowInteropHelper(this);
            Win32Message.SetWindowLong(
                helper.Handle,
                (int)Win32Message.GetWindowLongFields.GWL_EXSTYLE,
                (int)Win32Message.GetWindowLong(helper.Handle, (int)Win32Message.GetWindowLongFields.GWL_EXSTYLE) | (int)Win32Message.ExtendedWindowStyles.WS_EX_NOACTIVATE
                );
        }
    }
}
