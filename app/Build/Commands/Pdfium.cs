using System.Formats.Tar;
using System.IO.Compression;

using SharedTools;

namespace Build.Commands;

public static class Pdfium
{
    public static async Task InstallAsync(RID rid, string version, bool offline)
    {
        Console.Write($"- Installing Pdfium {version} for {rid.ToUserFriendlyName()} ...");

        var cwd = Environment.GetRustRuntimeDirectory();
        var pdfiumUrl = GetPdfiumDownloadUrl(rid, version);
        var library = GetLibraryPath(rid);
        var pdfiumLibTargetPath = Path.Join(cwd, "resources", "libraries", library.Filename);

        if (offline)
        {
            if (File.Exists(pdfiumLibTargetPath))
            {
                Console.WriteLine(" offline mode enabled and library already exists, skipping download");
                return;
            }

            Console.WriteLine($" failed because offline mode is enabled and '{pdfiumLibTargetPath}' does not exist");
            return;
        }

        if (string.IsNullOrWhiteSpace(library.Path))
        {
            Console.WriteLine($" failed to find the library path for {rid.ToUserFriendlyName()}");
            return;
        }

        var pdfiumLibTargetDirectory = Path.Join(cwd, "resources", "libraries");
        var pdfiumLibTmpTargetPath = Path.Join(pdfiumLibTargetDirectory, $"{library.Filename}.{Guid.NewGuid():N}.tmp");
        var pdfiumLibArchivePath = library.Path.Replace('\\', '/');

        //
        // Download the file:
        //
        Console.Write(" downloading ...");
        using var client = new HttpClient();
        using var response = await client.GetAsync(pdfiumUrl, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($" failed to download Pdfium {version} for {rid.ToUserFriendlyName()} from {pdfiumUrl}");
            return;
        }

        //
        // Extract the library from the downloaded file:
        //
        Console.Write(" extracting ...");
        Directory.CreateDirectory(pdfiumLibTargetDirectory);

        var foundLibrary = false;
        try
        {
            await using var downloadStream = await response.Content.ReadAsStreamAsync();
            await using var uncompressedStream = new GZipStream(downloadStream, CompressionMode.Decompress);
            await using var tarReader = new TarReader(uncompressedStream, false);

            while (await tarReader.GetNextEntryAsync(false) is { } entry)
            {
                if (!string.Equals(entry.Name.Replace('\\', '/'), pdfiumLibArchivePath, StringComparison.Ordinal))
                    continue;

                if (entry.DataStream == null)
                    break;

                await using var fileStream = File.Create(pdfiumLibTmpTargetPath);
                await entry.DataStream.CopyToAsync(fileStream);
                foundLibrary = true;
                break;
            }

            if (!foundLibrary)
            {
                Console.WriteLine($" failed to find the library file '{pdfiumLibArchivePath}' in the Pdfium archive");
                return;
            }

            Console.Write(" deploying ...");
            File.Move(pdfiumLibTmpTargetPath, pdfiumLibTargetPath, true);
        }
        finally
        {
            if (File.Exists(pdfiumLibTmpTargetPath))
                File.Delete(pdfiumLibTmpTargetPath);
        }

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