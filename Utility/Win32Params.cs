using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Utility
{
    public enum ScanSideLogo
    {
        SSL_FRONT = 1,
        SSL_BACK = 2,
        SSL_ANY = 3,
        SSL_SINGLE = 4,
        SSL_BACK_CAN_CANCEL = 5
    }

    /// <summary>
    /// 訊息名稱SCANMANGERX裡面附帶的wParam
    /// </summary>
    public enum SCAN_MSG
    {
        SCAN_MSG_TYPE_STATUS = 1,  //對應SCAN_STATUS
        SCAN_MSG_TYPE_PRECHECK_RTN_VALUE,  //WC8沒有用到
        SCAN_MSG_TYPE_SCAN_RTN_VALUE,  //對應XSCAN_RTN_VALUE
        SCAN_MSG_TYPE_CALIBRATE_RTN,  //WC8沒有用到
        SCAN_MSG_TYPE_SCAN_PROGRESS  //WC8沒有用到
    };

    /// <summary>
    /// 訊息名稱SCANMANGERX裡面附帶的lParam，對應wParam SCAN_MSG_TYPE_STATUS
    /// </summary>
    public enum SCAN_STATUS
    {
        SCAN_STATUS_PAPER_ON = 1,
        SCAN_STATUS_PAPER_OFF,
        SCAN_STATUS_MACHINE_ON,
        SCAN_STATUS_MACHINE_OFF,
        SCAN_STATUS_BUTTON_ON,
        SCAN_STATUS_COLOR_BUTTON_ON,
        SCAN_STATUS_CONNECTED_ON,
        SCAN_STATUS_CONNECTED_OFF,
        SCAN_STATUS_WIZARD_BUTTON_ON,
        SCAN_STATUS_SCANNER_DETECT_FINISH,
        SCAN_STATUS_SKIP_BACK_BY_USER
    };

    /// <summary>
    /// 訊息名稱SCANMANGERX裡面附帶的lParam，對應wParam SCAN_MSG_TYPE_SCAN_RTN_VALUE
    /// </summary>
    public enum XSCAN_RTN_VALUE
    {
        SCAN_RTN_SUCESS = 0,
        SCAN_RTN_INTERNAL_FAIL = 1,
        SCAN_RTN_FINISH_IMAGE = 2,
        SCAN_RTN_FINISH_CARD = 3,
        SCAN_RTN_PAPER_NOT_EXIST = 4,
        SCAN_RTN_CANCEL_BYUSER = 5,
        SCAN_RTN_PARAMETER_OUT_OF_RANGE = 6,
        SCAN_RTN_DRIVER_NOT_INSTALL = 7,
        SCAN_RTN_MEMORY_ALLOC_FAIL = 8,
        SCAN_RTN_PAPER_JAM = 9,
        SCAN_RTN_SCANNER_NOT_CONNECTED = 10,
        SCAN_RTN_SCANNER_NEED_CALIBRATE = 11,
        SCAN_RTN_FAIL_TO_SAVE_IMAGE = 12  // 注意：WC8沒有定義這一項
    };

    /// <summary>
    /// 校正專用的lparam，對應wparam SCAN_MSG_TYPE_CALIBRATE_RTN
    /// </summary>
    public enum XCALIBRATE_RTN_VALUE
    {
        CALIBRATE_RTN_SUCESS = 0,
        CALIBRATE_RTN_INTERNAL_FAIL = 1,
        CALIBRATE_RTN_FAIL = 2,
        CALIBRATE_RTN_WHITE_PAPER = 3,
        CALIBRATE_RTN_NO_RIGHT_TO_SAVE = 4,
        CALIBRATE_RTN_BUSY = 5,
        CALIBRATE_RTN_DEVICE_NOT_FOUND = 6,
        CALIBRATE_RTN_NO_PAPER = 7,
        CALIBRATE_RTN_PAPERJAM = 8,
        CALIBRATE_RTN_PAPER_GONE = 9
    };
}
