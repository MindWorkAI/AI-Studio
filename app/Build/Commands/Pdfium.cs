using System.Formats.Tar;
using System.IO.Compression;

using Build.Tools;

using SharedTools;

namespace Build.Commands;

public static class Pdfium
{
    private const int MAX_DOWNLOAD_ATTEMPTS = 3;

    private static readonly TimeSpan CONNECT_TIMEOUT = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RESPONSE_TIMEOUT = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan TRANSFER_TIMEOUT = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RETRY_DELAY = TimeSpan.FromSeconds(5);

    //
    // Without a handler of its own, HttpClient waits for a connection without any limit, so a single
    // connection attempt which hangs uses up the whole timeout of the request. That happened twice while
    // preparing v26.11.1, while curl fetched the same file within a second. The handler limits each
    // connection attempt, the TLS handshake included, and each download attempt has its own time budget:
    //
    private static readonly HttpClient CLIENT = new(new SocketsHttpHandler { ConnectTimeout = CONNECT_TIMEOUT })
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    /// <summary>
    /// Installs the Pdfium library of the given version into the resources of the Rust runtime.
    /// </summary>
    /// <remarks>
    /// Every failure ends the build: without the library, AI Studio cannot read PDF files, and Tauri would fail
    /// later with a far less clear message, because its resources include the folder of the library. The download
    /// is skipped when the library of this version is already installed, which a version marker in the target
    /// folder of the runtime records. The marker cannot sit next to the library, because Tauri ships everything
    /// in that folder.
    /// </remarks>
    /// <param name="rid">The RID to install the library for.</param>
    /// <param name="version">The Chromium revision of the Pdfium build, e.g., 7763.</param>
    /// <param name="offline">True, when the build must not download anything and uses the library already present.</param>
    public static async Task InstallAsync(RID rid, string version, bool offline)
    {
        Console.Write($"- Installing Pdfium {version} for {rid.UserFriendlyName} ...");

        var library = GetLibraryPath(rid);
        var pdfiumUrl = GetPdfiumDownloadUrl(rid, version);
        if (string.IsNullOrWhiteSpace(library.Path) || string.IsNullOrWhiteSpace(pdfiumUrl))
        {
            Console.WriteLine(" failed.");
            Console.WriteLine($"- Error: There is no Pdfium library for {rid.UserFriendlyName}.");
            throw new CommandExitedException(1);
        }

        var cwd = Environment.GetRustRuntimeDirectory();
        var pdfiumLibTargetPath = Path.Join(cwd, "resources", "libraries", library.Filename);
        var versionMarkerPath = Path.Join(cwd, "target", "pdfium.version");
        var versionMarker = $"{version} {rid.MicrosoftRid}";

        if (offline)
        {
            if (File.Exists(pdfiumLibTargetPath))
            {
                Console.WriteLine(" offline mode enabled and library already exists, skipping download");
                return;
            }

            Console.WriteLine(" failed.");
            Console.WriteLine($"- Error: Offline mode is enabled, but '{pdfiumLibTargetPath}' does not exist. Build once without --offline to download it.");
            throw new CommandExitedException(1);
        }

        if (File.Exists(pdfiumLibTargetPath) && await ReadVersionMarkerAsync(versionMarkerPath) == versionMarker)
        {
            Console.WriteLine(" already installed, skipping download");
            return;
        }

        for (var attempt = 1; attempt <= MAX_DOWNLOAD_ATTEMPTS; attempt++)
        {
            try
            {
                await DownloadAndDeployAsync(pdfiumUrl, library.Path.Replace('\\', '/'), pdfiumLibTargetPath, versionMarkerPath, versionMarker);
                Console.WriteLine(" done.");
                return;
            }
            catch (Exception exception) when (IsTransientFailure(exception))
            {
                Console.Write($" failed: {DescribeFailure(exception)}");
                if (attempt < MAX_DOWNLOAD_ATTEMPTS)
                {
                    Console.Write($" Retrying in {RETRY_DELAY.TotalSeconds:0} seconds ...");
                    await Task.Delay(RETRY_DELAY);
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine($"- Error: Could not download Pdfium {version} for {rid.UserFriendlyName} from {pdfiumUrl} in {MAX_DOWNLOAD_ATTEMPTS} attempts.");
        if (File.Exists(pdfiumLibTargetPath))
            Console.WriteLine("  To build with the library which is already present, use --offline.");

        throw new CommandExitedException(1);
    }

    /// <summary>
    /// Downloads the Pdfium archive once, extracts the library from it, and puts the library in place.
    /// </summary>
    /// <remarks>
    /// A failure which the next attempt might not run into, such as a broken connection or a timeout, leaves this
    /// method as an exception, which IsTransientFailure recognizes. A failure which every attempt would run into,
    /// such as an unknown version, ends the build right here.
    /// </remarks>
    /// <param name="pdfiumUrl">The address of the Pdfium archive.</param>
    /// <param name="pdfiumLibArchivePath">The path of the library inside the archive, with forward slashes.</param>
    /// <param name="pdfiumLibTargetPath">Where to put the library.</param>
    /// <param name="versionMarkerPath">Where to record which version the library has.</param>
    /// <param name="versionMarker">The version and RID of the library.</param>
    private static async Task DownloadAndDeployAsync(string pdfiumUrl, string pdfiumLibArchivePath, string pdfiumLibTargetPath, string versionMarkerPath, string versionMarker)
    {
        var pdfiumLibTmpTargetPath = $"{pdfiumLibTargetPath}.{Guid.NewGuid():N}.tmp";
        var receivedResponse = false;
        using var attemptTimeout = new CancellationTokenSource(RESPONSE_TIMEOUT);
        try
        {
            //
            // Download the file:
            //
            Console.Write(" downloading ...");
            using var response = await CLIENT.GetAsync(pdfiumUrl, HttpCompletionOption.ResponseHeadersRead, attemptTimeout.Token);
            receivedResponse = true;
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(" failed.");
                Console.WriteLine($"- Error: The download of {pdfiumUrl} failed with {(int)response.StatusCode} {response.ReasonPhrase}. Check the Pdfium version in metadata.txt.");
                throw new CommandExitedException(1);
            }

            //
            // Extract the library from the downloaded file:
            //
            attemptTimeout.CancelAfter(TRANSFER_TIMEOUT);
            Console.Write(" extracting ...");
            Directory.CreateDirectory(Path.GetDirectoryName(pdfiumLibTargetPath)!);

            var foundLibrary = false;
            await using var downloadStream = await response.Content.ReadAsStreamAsync(attemptTimeout.Token);
            await using var uncompressedStream = new GZipStream(downloadStream, CompressionMode.Decompress);
            await using var tarReader = new TarReader(uncompressedStream);

            while (await tarReader.GetNextEntryAsync(cancellationToken: attemptTimeout.Token) is { } entry)
            {
                if (!string.Equals(entry.Name.Replace('\\', '/'), pdfiumLibArchivePath, StringComparison.Ordinal))
                    continue;

                if (entry.DataStream == null)
                    break;

                await using var fileStream = File.Create(pdfiumLibTmpTargetPath);
                await entry.DataStream.CopyToAsync(fileStream, attemptTimeout.Token);
                foundLibrary = true;
                break;
            }

            if (!foundLibrary)
            {
                Console.WriteLine(" failed.");
                Console.WriteLine($"- Error: The Pdfium archive {pdfiumUrl} does not contain the library file '{pdfiumLibArchivePath}'.");
                throw new CommandExitedException(1);
            }

            //
            // Deploy the library. The marker goes first, so that it never describes a library
            // which was only half replaced:
            //
            Console.Write(" deploying ...");
            File.Delete(versionMarkerPath);
            File.Move(pdfiumLibTmpTargetPath, pdfiumLibTargetPath, true);
            Directory.CreateDirectory(Path.GetDirectoryName(versionMarkerPath)!);
            await File.WriteAllTextAsync(versionMarkerPath, versionMarker, Environment.UTF8_NO_BOM);
        }
        catch (OperationCanceledException) when (attemptTimeout.IsCancellationRequested)
        {
            throw new TimeoutException(receivedResponse
                ? $"The download did not finish within {TRANSFER_TIMEOUT.TotalMinutes:0} minutes."
                : $"There was no answer within {RESPONSE_TIMEOUT.TotalSeconds:0} seconds.");
        }
        catch (OperationCanceledException exception) when (exception.GetBaseException() is TimeoutException)
        {
            // The handler reports its connect timeout without the time it waited:
            throw new TimeoutException($"No connection could be established within {CONNECT_TIMEOUT.TotalSeconds:0} seconds.");
        }
        finally
        {
            if (File.Exists(pdfiumLibTmpTargetPath))
                File.Delete(pdfiumLibTmpTargetPath);
        }
    }

    /// <summary>
    /// Whether another attempt may succeed where a download attempt failed with the given exception.
    /// </summary>
    /// <param name="exception">The exception the attempt failed with.</param>
    /// <returns>True, when the download is worth another attempt.</returns>
    private static bool IsTransientFailure(Exception exception) => exception is HttpRequestException or OperationCanceledException or TimeoutException or IOException or InvalidDataException;

    /// <summary>
    /// Describes why a download attempt failed, in one line.
    /// </summary>
    /// <remarks>
    /// The innermost exception names the actual cause, such as the socket error behind an HttpRequestException
    /// or the timeout behind a canceled connection attempt, while the outer one often names the host.
    /// </remarks>
    /// <param name="exception">The exception the attempt failed with.</param>
    /// <returns>The reason of the failure.</returns>
    private static string DescribeFailure(Exception exception)
    {
        var innermost = exception.GetBaseException();
        if (innermost == exception || exception is OperationCanceledException)
            return innermost.Message;

        return $"{exception.Message} {innermost.Message}";
    }

    private static async Task<string> ReadVersionMarkerAsync(string versionMarkerPath)
    {
        if (!File.Exists(versionMarkerPath))
            return string.Empty;

        return (await File.ReadAllTextAsync(versionMarkerPath, Encoding.UTF8)).Trim();
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