using System.Diagnostics.Eventing.Reader;
using System.Formats.Tar;
using System.IO.Compression;

using SharedTools;

namespace Build.Commands;

public static class Qdrant
{
    public static async Task InstallAsync(RID rid, string version)
    {
        Console.Write($"- Installing Qdrant {version} for {rid.ToUserFriendlyName()} ...");

        var cwd = Environment.GetRustRuntimeDirectory();
        var qdrantTmpDownloadPath = Path.GetTempFileName();
        var qdrantTmpExtractPath = Directory.CreateTempSubdirectory();
        var qdrantUrl = GetQdrantDownloadUrl(rid, version);

        //
        // Download the file:
        //
        Console.Write(" downloading ...");
        using (var client = new HttpClient())
        {
            var response = await client.GetAsync(qdrantUrl);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($" failed to download Qdrant {version} for {rid.ToUserFriendlyName()} from {qdrantUrl}");
                return;
            }

            await using var fileStream = File.Create(qdrantTmpDownloadPath);
            await response.Content.CopyToAsync(fileStream);
        }
        
        //
        // Extract the downloaded file:
        //
        Console.Write(" extracting ...");
        await using(var zStream = File.Open(qdrantTmpDownloadPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (rid == RID.WIN_X64)
            {
                using var archive = new ZipArchive(zStream, ZipArchiveMode.Read);
                archive.ExtractToDirectory(qdrantTmpExtractPath.FullName, overwriteFiles: true);
            } else
            {
                await using var uncompressedStream = new GZipStream(zStream, CompressionMode.Decompress);
                await TarFile.ExtractToDirectoryAsync(uncompressedStream, qdrantTmpExtractPath.FullName, true);   
            }
        }
        
        //
        // Copy the database to the target directory:
        //
        Console.Write(" deploying ...");
        var database = GetDatabasePath(rid);
        if (string.IsNullOrWhiteSpace(database.Path))
        {
            Console.WriteLine($" failed to find the database path for {rid.ToUserFriendlyName()}");
            return;
        }
        
        var qdrantDBSourcePath = Path.Join(qdrantTmpExtractPath.FullName, database.Path);
        var qdrantDBTargetPath = Path.Join(cwd, "resources", "databases", "qdrant",database.Filename);
        if (!File.Exists(qdrantDBSourcePath))
        {
            Console.WriteLine($" failed to find the database file '{qdrantDBSourcePath}'");
            return;
        }
        
        Directory.CreateDirectory(Path.Join(cwd, "resources", "databases", "qdrant"));
        if (File.Exists(qdrantDBTargetPath))
            File.Delete(qdrantDBTargetPath);
        
        File.Copy(qdrantDBSourcePath, qdrantDBTargetPath);
        
        //
        // Cleanup:
        //
        Console.Write(" cleaning up ...");
        File.Delete(qdrantTmpDownloadPath);
        Directory.Delete(qdrantTmpExtractPath.FullName, true);
        
        Console.WriteLine(" done.");
    }
    
    private static Database GetDatabasePath(RID rid) => rid switch
    {
        RID.OSX_ARM64 => new("qdrant", "qdrant-aarch64-apple-darwin"),
        RID.OSX_X64 => new("qdrant", "qdrant-x86_64-apple-darwin"),
        
        RID.LINUX_ARM64 => new("qdrant", "qdrant-aarch64-unknown-linux-musl"),
        RID.LINUX_X64 => new("qdrant", "qdrant-x86_64-unknown-linux-gnu"),
        
        RID.WIN_X64 => new("qdrant.exe", "qdrant-x86_64-pc-windows-msvc.exe"),
        RID.WIN_ARM64 => new("qdrant.exe", "qdrant-aarch64-pc-windows-msvc.exe"),
        
        _ => new(string.Empty, string.Empty),
    };

    private static string GetQdrantDownloadUrl(RID rid, string version)
    {
        var baseUrl = $"https://github.com/qdrant/qdrant/releases/download/v{version}/qdrant-";
        return rid switch
        {
            RID.LINUX_ARM64 => $"{baseUrl}aarch64-unknown-linux-musl.tar.gz",
            RID.LINUX_X64 => $"{baseUrl}x86_64-unknown-linux-gnu.tar.gz",
            
            RID.OSX_ARM64 => $"{baseUrl}aarch64-apple-darwin.tar.gz",
            RID.OSX_X64 => $"{baseUrl}x86_64-apple-darwin.tar.gz",
            
            RID.WIN_X64 => $"{baseUrl}x86_64-pc-windows-msvc.zip",
            RID.WIN_ARM64 => $"{baseUrl}x86_64-pc-windows-msvc.zip",
            
            _ => string.Empty,
        };
    }
}