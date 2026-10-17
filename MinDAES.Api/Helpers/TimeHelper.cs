using TimeZoneConverter;

namespace MinDAES.Api.Helpers
{
    public static class TimeHelper
    {
        public static DateTime GetPhilippineTime()
        {
            //TimeZoneInfo phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
            //DateTime phTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, phTimeZone);
            //return phTime;
            string timeZoneId = TZConvert.IanaToWindows("Asia/Manila");
            TimeZoneInfo phTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            DateTime phTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, phTimeZone);
            return phTime;
        }

        public static string FormatEventDates(DateTime eventStartDate, DateTime eventEndDate, bool eventIsAllDay)
        {
            var isSameDay = eventStartDate.Date == eventEndDate.Date;

            if (eventIsAllDay && isSameDay)
            {
                return $"{eventStartDate:MMMM d, yyyy} (all-day)";
            }

            if (!eventIsAllDay && isSameDay)
            {
                return $"{eventStartDate:MMMM d, yyyy}, {eventStartDate:h:mm tt} - {eventEndDate:h:mm tt}";
            }

            if (!isSameDay)
            {
                return $"{eventStartDate:MMMM d} - {eventEndDate:dd}, {eventStartDate:yyyy}";
            }

            return string.Empty;
        }

        public static string FormatRegularDates(DateTime regularDate)
        {
            return $"{regularDate:MMMM d, yyyy}";
        }

        public static string GetTimeAgo(DateTime eventTime, DateTime currentTime)
        {
            var timeSpan = currentTime - eventTime;
            if (timeSpan.TotalMinutes < 60)
                return $"{timeSpan.Minutes} mins ago";
            if (timeSpan.TotalHours < 2)
                return $"{timeSpan.Hours} hour ago";
            if (timeSpan.TotalHours < 24)
                return $"{timeSpan.Hours} hour/s ago";
            else
                //return eventTime.ToString("MMM dd, yyyy");
                return eventTime.ToString("hh:mm tt");
        }
    }
}