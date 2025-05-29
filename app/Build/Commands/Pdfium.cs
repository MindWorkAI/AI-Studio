using System.Formats.Tar;
using System.IO.Compression;

using SharedTools;

namespace Build.Commands;

public static class Pdfium
{
    public static async Task InstallAsync(RID rid, string version)
    {
        Console.Write($"- Installing Pdfium {version} for {rid.ToUserFriendlyName()} ...");

        var cwd = Environment.GetRustRuntimeDirectory();
        var pdfiumTmpDownloadPath = Path.GetTempFileName();
        var pdfiumTmpExtractPath = Directory.CreateTempSubdirectory();
        var pdfiumUrl = GetPdfiumDownloadUrl(rid, version);

        //
        // Download the file:
        //
        Console.Write(" downloading ...");
        using (var client = new HttpClient())
        {
            var response = await client.GetAsync(pdfiumUrl);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($" failed to download Pdfium {version} for {rid.ToUserFriendlyName()} from {pdfiumUrl}");
                return;
            }

            await using var fileStream = File.Create(pdfiumTmpDownloadPath);
            await response.Content.CopyToAsync(fileStream);
        }
        
        //
        // Extract the downloaded file:
        //
        Console.Write(" extracting ...");
        await using(var tgzStream = File.Open(pdfiumTmpDownloadPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await using var uncompressedStream = new GZipStream(tgzStream, CompressionMode.Decompress);
            await TarFile.ExtractToDirectoryAsync(uncompressedStream, pdfiumTmpExtractPath.FullName, true);
        }
        
        //
        // Copy the library to the target directory:
        //
        Console.Write(" deploying ...");
        var library = GetLibraryPath(rid);
        if (string.IsNullOrWhiteSpace(library.Path))
        {
            Console.WriteLine($" failed to find the library path for {rid.ToUserFriendlyName()}");
            return;
        }
        
        var pdfiumLibSourcePath = Path.Join(pdfiumTmpExtractPath.FullName, library.Path);
        var pdfiumLibTargetPath = Path.Join(cwd, "resources", "libraries", library.Filename);
        if (!File.Exists(pdfiumLibSourcePath))
        {
            Console.WriteLine($" failed to find the library file '{pdfiumLibSourcePath}'");
            return;
        }
        
        Directory.CreateDirectory(Path.Join(cwd, "resources", "libraries"));
        if (File.Exists(pdfiumLibTargetPath))
            File.Delete(pdfiumLibTargetPath);
        
        File.Copy(pdfiumLibSourcePath, pdfiumLibTargetPath);
        
        //
        // Cleanup:
        //
        Console.Write(" cleaning up ...");
        File.Delete(pdfiumTmpDownloadPath);
        Directory.Delete(pdfiumTmpExtractPath.FullName, true);
        
        Console.WriteLine(" done.");
    }
    
    private static Library GetLibraryPath(RID rid) => rid switch
    {
        RID.LINUX_ARM64 or RID.LINUX_X64 => new(Path.Join("lib", "libpdfium.so"), "libpdfium.so"),
        RID.OSX_ARM64 or RID.OSX_X64 => new(Path.Join("lib", "libpdfium.dylib"), "libpdfium.dylib"),
        RID.WIN_ARM64 or RID.WIN_X64 => new(Path.Join("bin", "pdfium.dll"), "pdfium.dll"),
        
        _ => new(string.Empty, string.Empty),
    };

    private static string GetPdfiumDownloadUrl(RID rid, string version)
    {
        var baseUrl = $"https://github.com/bblanchon/pdfium-binaries/releases/download/chromium%2F{version}/pdfium-";
        return rid switch
        {
            RID.LINUX_ARM64 => $"{baseUrl}linux-arm64.tgz",
            RID.LINUX_X64 => $"{baseUrl}linux-x64.tgz",
            
            RID.OSX_ARM64 => $"{baseUrl}mac-arm64.tgz",
            RID.OSX_X64 => $"{baseUrl}mac-x64.tgz",
            
            RID.WIN_ARM64 => $"{baseUrl}win-arm64.tgz",
            RID.WIN_X64 => $"{baseUrl}win-x64.tgz",
            
            _ => string.Empty,
        };
    }
}