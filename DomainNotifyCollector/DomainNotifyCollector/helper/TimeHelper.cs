﻿using System;

namespace ContractNotifyCollector.helper
{
    /// <summary>
    /// 时间帮助类
    /// 
    /// </summary>
    class TimeHelper
    {
        public static long GetTimeStamp()
        {
            TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return Convert.ToInt64(ts.TotalSeconds);
        }

        public static string toBlockindexTimeFmt(long blockindexTime)
        {
            DateTime st = TimeZoneInfo.ConvertTime(new DateTime(1970, 1, 1, 0, 0, 0, 0), TimeZoneInfo.Local);
            return st.AddHours(8).AddSeconds(blockindexTime).ToString();
        }
    }
}
