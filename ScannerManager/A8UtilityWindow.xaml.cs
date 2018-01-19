using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TwainDotNet;

namespace ScannerManager
{
    public enum A8Utitlty
    {
        IsPaperOn,
        IsNeedCalibrate,
        DoCalibrate
    }

    /// <summary>
    /// A8PaperOnWindow.xaml 的互動邏輯
    /// </summary>
    public partial class A8UtilityWindow : Window
    {
        //public bool IsNeedCalibrate = false;
        //public bool IsPaperOn = false;
        public A8Result Result = A8Result.Fail;

        private A8Utitlty _func;

        public A8UtilityWindow(A8Utitlty func)
        {
            InitializeComponent();
            Loaded += A8PaperOnWindow_Loaded;
            _func = func;
            //Initialized += A8PaperOnWindow_Initialized;
            //Closing += A8PaperOnWindow_Closing;

            ShowActivated = false;
            //IsPaperOn = false;
            //IsNeedCalibrate = false;
            Left = -100;
            Top = -100;
        }

        private void A8PaperOnWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Console.WriteLine("A8PaperOnWindow closing...");
        }

        private void A8PaperOnWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Twain twain = null;
            try
            {
                twain = new Twain(new WpfWindowMessageHook(this));
                twain.SelectSource("A8 ColorScanner PP");
                bool bReturn = false;
                switch (_func)
                {
                    case A8Utitlty.IsPaperOn:
                        bReturn = twain.IsPaperOn();
                        break;
                    case A8Utitlty.IsNeedCalibrate:
                        bReturn = twain.A8NeedCalibrate();
                        break;
                    case A8Utitlty.DoCalibrate:
                        bReturn = twain.CalibrateA8();
                        break;
                }
                Result = bReturn ? A8Result.Success_True : A8Result.Success_False;
                twain.Dispose();
            }
            catch (DeviceOpenExcetion ex)
            {
                Utility.Logger.WriteLog("ScannerManager", Utility.LOG_LEVEL.LL_SUB_FUNC, "A8 utility window device open: " + ex);
                twain?.Dispose();
                Result = A8Result.ScannerOpenFail;
            }
            catch (FeederEmptyException)
            {
                twain?.Dispose();
                Result = A8Result.FeederEmpty;
            }
            catch (TwainException ex)
            {
                Console.WriteLine(ex);
                Utility.Logger.WriteLog("ScannerManager", Utility.LOG_LEVEL.LL_SUB_FUNC, "A8 utility window Twain exception: " + ex);
                twain?.Dispose();
                Result = A8Result.OtherTwainException;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Utility.Logger.WriteLog("ScannerManager", Utility.LOG_LEVEL.LL_SUB_FUNC, "A8 utility window exception: " + ex);
                twain?.Dispose();
                Result = A8Result.Fail;
            }
            Close();
        }
    }
}
