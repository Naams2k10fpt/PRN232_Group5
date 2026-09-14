namespace QuizManagement.Helpers;

public static class VietnamTime
{
    private static readonly TimeZoneInfo DisplayTimeZone = TimeZoneInfo.CreateCustomTimeZone(
        "Asia/Ho_Chi_Minh",
        TimeSpan.FromHours(7),
        "Vietnam Time",
        "Vietnam Time");

    public static DateTime FromUtc(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        // ponytail: the product currently has one display zone and no user timezone preference.
        return TimeZoneInfo.ConvertTimeFromUtc(utc, DisplayTimeZone);
    }
}
