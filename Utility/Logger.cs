using System;
using System.IO;

namespace Utility
{
    public class Logger
    {
        private const string LOGPATH = @"\Penpower\ScannerManager\";
        private static object m_sLockFlag = new object();

        public static void WriteLog(string FileName, LOG_LEVEL llLogLevel, string LogStr)
        {
            //時間、iLevel、字串，符合Level設定範圍的就寫入log

            string strExt = Path.GetExtension(FileName);
            if (strExt == null || strExt.Length == 0)
                strExt = ".log";

            string strFile = Path.GetFileNameWithoutExtension(FileName);

            string LogPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + LOGPATH;
            string FilePath = LogPath + strFile + DateTime.Now.ToString("yyyyMMdd") + strExt;
            
            // todo: 依照設定顯示不同的log level
            //if (IsNeedWritelog(envirObj, llLogLevel) == true)
            {
                if (!Directory.Exists(LogPath))
                {
                    Directory.CreateDirectory(LogPath);
                }

                lock (m_sLockFlag)
                {
                    try
                    {
                        StreamWriter sw = null;
                        sw = File.AppendText(FilePath);

                        string LevelStr = "(str)";
                        LevelStr = LevelStr.Replace("str", llLogLevel.ToString());

                        sw.WriteLine("{0} {1} {2}",                                 //[時間]  Level  ErrMsg
                            DateTime.Now.ToString("[yyyy/MM/dd HH:mm:ss:fff]"),
                            LevelStr, LogStr);
                        sw.Flush();
                        if (sw != null) sw.Close();
                    }
                    catch (Exception e)
                    {
                        e.Message.ToString();
                    }
                }
            }
        }
    }

    public enum LOG_LEVEL
    {
        LL_SERIOUS_ERROR = 1,
        LL_SUB_FUNC = 2,
        LL_NORMAL_LOG = 3,
        LL_TRACE_LOG = 4
    }
}
