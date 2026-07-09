using System.IO.Compression;
using System.Net.Http.Headers;

namespace AIStudio.Tools.PluginSystem;

public static partial class PluginFactory
{
    public static async Task<(bool Success, EntityTagHeaderValue? ETag, string? Issue)> DetermineConfigPluginETagAsync(Guid configPlugId, string configServerUrl, CancellationToken cancellationToken = default)
    {
        if(configPlugId == Guid.Empty || string.IsNullOrWhiteSpace(configServerUrl))
            return (false, null, "Configuration ID or server URL is missing.");
        
        try
        {
            var serverUrl = configServerUrl.EndsWith('/') ? configServerUrl[..^1] : configServerUrl;
            var downloadUrl = $"{serverUrl}/{configPlugId}.zip";
            
            using var http = ExternalHttpClientTimeout.CreateHttpClient(ExternalHttpTrustPolicy.ALLOW_CUSTOM_ROOTS_WHEN_HOST_WHITELISTED);
            using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                LOG.LogError($"Failed to determine the ETag for configuration plugin '{configPlugId}'. HTTP Status: {response.StatusCode}");
                return (false, null, $"HTTP status: {response.StatusCode}");
            }

            return (true, response.Headers.ETag, null);
        }
        catch (Exception e)
        {
            LOG.LogError(e, "An error occurred while determining the ETag for the configuration plugin.");
            return (false, null, e.Message);
        }
    }
    
    public static async Task<bool> TryDownloadingConfigPluginAsync(Guid configPlugId, string configServerUrl, CancellationToken cancellationToken = default)
    {
        if(!IsInitialized)
        {
            LOG.LogWarning("Plugin factory is not yet initialized. Cannot download configuration plugin.");
            return false;
        }

        var serverUrl = configServerUrl.EndsWith('/') ? configServerUrl[..^1] : configServerUrl;
        var downloadUrl = $"{serverUrl}/{configPlugId}.zip";
        
        LOG.LogInformation($"Try to download configuration plugin with ID='{configPlugId}' from server='{configServerUrl}' (GET {downloadUrl})");
        var tempDownloadFile = Path.GetTempFileName();
        var stagedDirectory = Path.Join(CONFIGURATION_PLUGINS_ROOT, $"{configPlugId}.staging-{Guid.NewGuid():N}");
        string? backupDirectory = null;
        var wasSuccessful = false;
        try
        {
            await LockHotReloadAsync();
            using var httpClient = ExternalHttpClientTimeout.CreateHttpClient(ExternalHttpTrustPolicy.ALLOW_CUSTOM_ROOTS_WHEN_HOST_WHITELISTED);
            var response = await httpClient.GetAsync(downloadUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                LOG.LogError($"Failed to download the enterprise configuration plugin. HTTP Status: {response.StatusCode}");
                return false;
            }

            await using(var tempFileStream = File.Create(tempDownloadFile))
            {
                await response.Content.CopyToAsync(tempFileStream, cancellationToken);
            }

            ExtractConfigPluginArchive(tempDownloadFile, stagedDirectory);

            var configDirectory = Path.Join(CONFIGURATION_PLUGINS_ROOT, configPlugId.ToString());
            if (Directory.Exists(configDirectory))
            {
                backupDirectory = Path.Join(CONFIGURATION_PLUGINS_ROOT, $"{configPlugId}.backup-{Guid.NewGuid():N}");
                Directory.Move(configDirectory, backupDirectory);
            }

            Directory.Move(stagedDirectory, configDirectory);
            if (!string.IsNullOrWhiteSpace(backupDirectory) && Directory.Exists(backupDirectory))
                Directory.Delete(backupDirectory, true);

            LOG.LogInformation($"Configuration plugin with ID='{configPlugId}' downloaded and extracted successfully to '{configDirectory}'.");
            wasSuccessful = true;
        }
        catch (Exception e)
        {
            LOG.LogError(e, "An error occurred while downloading or extracting the enterprise configuration plugin.");

            var configDirectory = Path.Join(CONFIGURATION_PLUGINS_ROOT, configPlugId.ToString());
            if (!string.IsNullOrWhiteSpace(backupDirectory) && Directory.Exists(backupDirectory) && !Directory.Exists(configDirectory))
            {
                try
                {
                    Directory.Move(backupDirectory, configDirectory);
                }
                catch (Exception restoreException)
                {
                    LOG.LogError(restoreException, "Failed to restore the previous configuration plugin after a failed update.");
                }
            }
        }
        finally
        {
            if (Directory.Exists(stagedDirectory))
            {
                try
                {
                    Directory.Delete(stagedDirectory, true);
                }
                catch (Exception e)
                {
                    LOG.LogError(e, "Failed to delete the staged configuration plugin directory.");
                }
            }

            if (File.Exists(tempDownloadFile))
            {
                try
                {
                    File.Delete(tempDownloadFile);
                }
                catch (Exception e)
                {
                    LOG.LogError(e, "Failed to delete the temporary download file.");
                }
            }
            
            UnlockHotReload();
        }
        
        return wasSuccessful;
    }

    // Compatibility shim for Windows-created ZIPs with backslashes in entry names (dotnet/runtime#27620).
    // See documentation/compatibility-shims/2026-07-enterprise-config-zip-backslashes.md.
    private static void ExtractConfigPluginArchive(string sourceArchiveFileName, string destinationDirectory)
    {
        using var archive = ZipFile.OpenRead(sourceArchiveFileName);
        Directory.CreateDirectory(destinationDirectory);

        var destinationDirectoryFullPath = Path.GetFullPath(destinationDirectory);
        if (!destinationDirectoryFullPath.EndsWith(Path.DirectorySeparatorChar))
            destinationDirectoryFullPath += Path.DirectorySeparatorChar;

        foreach (var entry in archive.Entries)
        {
            var normalizedEntryName = NormalizeConfigPluginZipEntryName(entry.FullName);
            var destinationPath = GetConfigPluginZipEntryDestinationPath(destinationDirectoryFullPath, normalizedEntryName);

            if (normalizedEntryName.EndsWith('/'))
            {
                if (entry.Length != 0)
                    throw new InvalidDataException($"The enterprise configuration plugin archive contains a directory entry with data: '{entry.FullName}'.");

                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            entry.ExtractToFile(destinationPath);
        }

        if (!Directory.EnumerateFiles(destinationDirectory, "plugin.lua", SearchOption.AllDirectories).Any())
            throw new InvalidDataException("The enterprise configuration plugin archive does not contain a plugin.lua file.");
    }

    private static string NormalizeConfigPluginZipEntryName(string entryName)
    {
        var normalizedEntryName = entryName.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalizedEntryName))
            throw new InvalidDataException("The enterprise configuration plugin archive contains an empty entry name.");

        if (normalizedEntryName.Contains('\0'))
            throw new InvalidDataException($"The enterprise configuration plugin archive contains an invalid entry name: '{entryName}'.");

        if (normalizedEntryName.StartsWith('/'))
            throw new InvalidDataException($"The enterprise configuration plugin archive contains a rooted entry name: '{entryName}'.");

        if (normalizedEntryName is [_, ':', ..])
            throw new InvalidDataException($"The enterprise configuration plugin archive contains a drive-qualified entry name: '{entryName}'.");

        var pathSegments = normalizedEntryName.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (pathSegments.Length == 0 || pathSegments.Any(segment => segment is "." or ".."))
            throw new InvalidDataException($"The enterprise configuration plugin archive contains an unsafe entry name: '{entryName}'.");

        return normalizedEntryName;
    }

    private static string GetConfigPluginZipEntryDestinationPath(string destinationDirectoryFullPath, string normalizedEntryName)
    {
        var pathSegments = normalizedEntryName.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var relativePath = Path.Combine(pathSegments);
        var destinationPath = Path.GetFullPath(Path.Combine(destinationDirectoryFullPath, relativePath));
        if (!destinationPath.StartsWith(destinationDirectoryFullPath, StringComparison.Ordinal))
            throw new InvalidDataException($"The enterprise configuration plugin archive contains an entry outside the destination directory: '{normalizedEntryName}'.");

        return destinationPath;
    }
}