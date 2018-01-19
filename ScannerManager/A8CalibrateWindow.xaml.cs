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
    /// <summary>
    /// A8CalibrateWindow.xaml 的互動邏輯
    /// </summary>
    public partial class A8CalibrateWindow : Window
    {
        public A8CalibrateWindow()
        {
            InitializeComponent();
            Loaded += A8CalibrateWindow_Loaded; ;
            //Closing += A8CalibrateWindow_Closing; ;

            ShowActivated = false;
            Left = -100;
            Top = -100;
        }

        private void A8CalibrateWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void A8CalibrateWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var twain = new Twain(new WpfWindowMessageHook(this));
                twain.SelectSource("A8 ColorScanner PP");
                // todo: A8怎麼做校正？                
                //twain.c
                twain.Dispose();
                throw new NotImplementedException();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            Close();
        }
    }
}
