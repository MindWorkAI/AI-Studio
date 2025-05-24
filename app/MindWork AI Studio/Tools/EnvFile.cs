#if DEBUG
using System.Text;

namespace AIStudio.Tools;

/// <summary>
/// Read environment variables for the application from an .env file.
/// </summary>
/// <remarks>
/// We consider this feature a security issue. Therefore, it is only
/// available in DEBUG mode. To ensure this, we remove the code
/// from any release build.
/// </remarks>
public static class EnvFile
{
    public static async Task Apply(string filePath)
    {
        if(!File.Exists(filePath))
        {
            Console.WriteLine($"Error: The .env file '{filePath}' does not exist.");
            return;
        }
        
        var lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.Trim().StartsWith('#'))
                continue;

            var parts = line.Split(['='], 2);
            if (parts.Length != 2)
                continue;

            var key = parts[0].Trim();
            var value = parts[1].Trim();

            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
#endif