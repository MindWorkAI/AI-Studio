using System.Globalization;

namespace AIStudio.Tools.Databases.IndexStore;

internal static class IndexStoreDateTimeOffset
{
    public static string ToUtcText(DateTimeOffset dateTime)
    {
        return dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    public static DateTimeOffset ParseUtc(string value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime)
            ? dateTime.ToUniversalTime()
            : DateTimeOffset.UnixEpoch;
    }
}
