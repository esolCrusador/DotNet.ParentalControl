namespace DotNet.ParentalControl.Extensions
{
    public static class DateTimeExtensions
    {
        public static DateTime Max(DateTime date1, DateTime date2) => date1 > date2 ? date1 : date2;
        public static DateTime Min(DateTime date1, DateTime date2) => date1 < date2 ? date1 : date2;

        public static DateTime Max(DateTime? date1, DateTime date2) => date1.HasValue ? Max(date1.Value, date2) : date2;
        public static DateTime Min(DateTime? date1, DateTime date2) => date1.HasValue ? Min(date1.Value, date2) : date2;

        public static DateTime Max(DateTime date1, DateTime? date2) => date2.HasValue ? Max(date1, date2.Value) : date1;
        public static DateTime Min(DateTime date1, DateTime? date2) => date2.HasValue ? Min(date1, date2.Value) : date1;

        public static bool IsBetween(this DateTime dateTime, DateTime start, DateTime end) =>
            start <= dateTime && dateTime <= end;
    }
}
