using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Utility
{
    public class StorageChecker
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetDiskFreeSpaceEx(string lpDirectoryName,
            out ulong lpFreeBytesAvailable,
            out ulong lpTotalNumberOfBytes,
            out ulong lpTotalNumberOfFreeBytes);

        /// <summary>
        /// 檢查是否可用空間小於100MB，不會跳出提示
        /// </summary>
        /// <returns>回傳false代表少於100MB，或是讀取失敗</returns>
        public static bool CheckDiskFreeSpace(string folderName)
        {
            if (!folderName.EndsWith("\\"))
            {
                folderName += '\\';
            }
            ulong free = 0, dummy1 = 0, dummy2 = 0;
            if (GetDiskFreeSpaceEx(folderName, out free, out dummy1, out dummy2))
            {
                ulong megaByte = free / 1048576;  //byte→MB
                if (megaByte < 100)
                {
                    return false;
                }
                else
                    return true;
            }
            else
                return false;
        }
    }
}
