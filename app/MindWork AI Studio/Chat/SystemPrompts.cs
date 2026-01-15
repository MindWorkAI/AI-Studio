using System.Globalization;

namespace AIStudio.Chat;

public static class SystemPrompts
{
    public static string Default
    {
        get
        {
            var nowUtc = DateTime.UtcNow;
            var nowLocal = DateTime.Now;
            
            return string.Create(
                new CultureInfo("en-US"),
                $"Today is {nowUtc:MMMM d, yyyy h:mm tt} (UTC) and {nowLocal:MMMM d, yyyy h:mm tt} (local time)."
            );
        }
    }
}