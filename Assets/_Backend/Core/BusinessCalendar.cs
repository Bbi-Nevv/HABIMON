using System;
using System.Globalization;

namespace HABIMON.Core
{
    public static class BusinessCalendar
    {
        public static DateTime GetDate(DateTimeOffset utcInstant)
        {
            return utcInstant.ToOffset(TimeSpan.FromHours(7)).Date;
        }

        public static string GetDateKey(DateTimeOffset utcInstant)
        {
            return GetDate(utcInstant).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }
}
