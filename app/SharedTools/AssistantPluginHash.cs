using System.Security.Cryptography;
using System.Text;

namespace SharedTools;

/// <summary>
/// Computes the canonical assistant-plugin hash across all Lua files in a plugin directory.
/// </summary>
public static class AssistantPluginHash
{
    public static string Compute(string pluginDirectory)
    {
        if (string.IsNullOrWhiteSpace(pluginDirectory) || !Directory.Exists(pluginDirectory))
            return string.Empty;

        var luaFiles = Directory.EnumerateFiles(pluginDirectory, "*.lua", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
        if (luaFiles.Count == 0)
            return string.Empty;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        foreach (var filePath in luaFiles)
        {
            var relativePath = Path.GetRelativePath(pluginDirectory, filePath).Replace('\\', '/');
            var pathBytes = Encoding.UTF8.GetBytes(relativePath);
            var contentBytes = File.ReadAllBytes(filePath);

            writer.Write(pathBytes.Length);
            writer.Write(pathBytes);
            writer.Write(contentBytes.Length);
            writer.Write(contentBytes);
        }

        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}
