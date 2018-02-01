using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ScannerManager
{
    public class Win32Message
    {
        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        public static extern uint RegisterWindowMessage(string msgName);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        public static extern IntPtr IntSetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        public static extern Int32 IntSetWindowLong(IntPtr hWnd, int nIndex, Int32 dwNewLong);

        [DllImport("kernel32.dll", EntryPoint = "SetLastError")]
        public static extern void SetLastError(int dwErrorCode);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

        [DllImport(@"AutoCrop Wrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern short AutoCrop([MarshalAs(UnmanagedType.LPWStr)] string strFileName);
        // see https://stackoverflow.com/questions/4608876/c-sharp-dllimport-with-c-boolean-function-not-returning-correctly

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool SetDllDirectory(string lpPathName);
        // see https://stackoverflow.com/questions/8836093/how-can-i-specify-a-dllimport-path-at-runtime
        // use it to set the directory for dllimport

        [DllImport("User32.dll")]
        public static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("User32.dll")]
        public static extern void ReleaseDC(IntPtr hwnd, IntPtr dc);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [Flags]
        public enum ExtendedWindowStyles
        {
            // ...
            WS_EX_TOOLWINDOW = 0x00000080,  // The window is intended to be used as a floating toolbar. 
            WS_EX_NOACTIVATE = 0x08000000,  // A top-level window created with this style does not become the foreground window when the user clicks it. 
            // ...
        }

        public enum GetWindowLongFields
        {
            // ...
            GWL_EXSTYLE = (-20),
            // ...
        }

        /// <summary>
        /// 將clientRect偏移，符合視窗的位置
        /// </summary>
        /// <param name="clientHwnd"></param>
        /// <param name="clientRect"></param>
        /// <returns></returns>
        public static Rect CalculateClientRect(IntPtr clientHwnd, Rect clientRect)
        {
            RECT rct = new RECT();
            if (GetWindowRect(clientHwnd, ref rct))
            {
                clientRect.X += rct.Left;
                clientRect.Y += rct.Top;
            }
            return clientRect;
        }

        /// <summary>
        /// Returns "%program files (x86)%\Penpower\iScan2\Bin\"
        /// </summary>
        public static string IscanPath
        {
            get
            {
                // see https://stackoverflow.com/questions/194157/c-sharp-how-to-get-program-files-x86-on-windows-64-bit
                string programFiles = Environment.GetEnvironmentVariable("PROGRAMFILES(X86)") ??
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                return programFiles + @"\Penpower\iScan2\Bin\";
            }
        }
    }
}
