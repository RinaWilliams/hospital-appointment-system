using System.Globalization;

namespace HospitalApi.Services
{
    public static class DateTimeParsing
    {
        public static bool TryParseDate(string value, out DateOnly date)
        {
            return DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }

        public static bool TryParseTime(string value, out TimeSpan time)
        {
            return TimeSpan.TryParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture, out time)
                || TimeSpan.TryParseExact(value, @"h\:mm", CultureInfo.InvariantCulture, out time);
        }
    }
}
