using System.Diagnostics;

using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    public async Task<DirectorySelectionResponse> SelectDirectory(string title, string? initialDirectory = null)
    {
        var encodedTitle = Uri.EscapeDataString(title);
        var result = initialDirectory is null
            ? await this.http.PostAsync($"/select/directory?title={encodedTitle}", null)
            : await this.http.PostAsJsonAsync($"/select/directory?title={encodedTitle}", new PreviousDirectory(initialDirectory), this.jsonRustSerializerOptions);
        
        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to select a directory: '{result.StatusCode}'");
            return new DirectorySelectionResponse(true, string.Empty);
        }
        
        return await result.Content.ReadFromJsonAsync<DirectorySelectionResponse>(this.jsonRustSerializerOptions);
    }
    
    public async Task<FileSelectionResponse> SelectFile(string title, FileTypeFilter[]? filter = null, string? initialFile = null)
    {
        var payload = new SelectFileOptions
        {
            Title = title,
            PreviousFile = initialFile is null ? null : new (initialFile),
            Filter = FileTypes.AsOneFileType(filter)
        };

        var result = await this.http.PostAsJsonAsync("/select/file", payload, this.jsonRustSerializerOptions);
        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to select a file: '{result.StatusCode}'");
            return new FileSelectionResponse(true, string.Empty);
        }

        return await result.Content.ReadFromJsonAsync<FileSelectionResponse>(this.jsonRustSerializerOptions);
    }

    public async Task<FilesSelectionResponse> SelectFiles(string title, FileTypeFilter[]? filter = null, string? initialFile = null)
    {
        var payload = new SelectFileOptions
        {
            Title = title,
            PreviousFile = initialFile is null ? null : new (initialFile),
            Filter = FileTypes.AsOneFileType(filter)
        };

        var result = await this.http.PostAsJsonAsync("/select/files", payload, this.jsonRustSerializerOptions);
        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to select files: '{result.StatusCode}'");
            return new FilesSelectionResponse(true, Array.Empty<string>());
        }

        return await result.Content.ReadFromJsonAsync<FilesSelectionResponse>(this.jsonRustSerializerOptions);
    }

    /// <summary>
    /// Initiates a dialog to let the user select a file for a writing operation.
    /// </summary>
    /// <param name="title">The title of the save file dialog.</param>
    /// <param name="filter">Optional file type filters for filtering specific file formats.</param>
    /// <param name="initialFile">An optional initial file path to pre-fill in the dialog.</param>
    /// <returns>A <see cref="FileSaveResponse"/> object containing information about whether the user canceled the
    /// operation and whether the select operation was successful.</returns>
    public async Task<FileSaveResponse> SaveFile(string title, FileTypeFilter[]? filter = null, string? initialFile = null)
    {
        var payload = new SaveFileOptions
        {
            Title = title,
            PreviousFile = initialFile is null ? null : new (initialFile),
            Filter = FileTypes.AsOneFileType(filter)
        };
        
        var result = await this.http.PostAsJsonAsync("/save/file", payload, this.jsonRustSerializerOptions);
        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to select a file for writing operation '{result.StatusCode}'");
            return new FileSaveResponse(true, string.Empty);
        }
        
        return await result.Content.ReadFromJsonAsync<FileSaveResponse>(this.jsonRustSerializerOptions);
    }

    public async Task<OpenPathResponse> OpenPathInFileManager(string path)
    {
        var runtimeResponse = await this.TryOpenPathInRuntimeFileManager(path);
        if (runtimeResponse.Success)
            return runtimeResponse;

        var localResponse = this.TryOpenPathInLocalFileManager(path);
        if (localResponse.Success)
            return localResponse;

        var issue = string.IsNullOrWhiteSpace(runtimeResponse.Issue)
            ? localResponse.Issue
            : $"{runtimeResponse.Issue} {localResponse.Issue}";

        return new OpenPathResponse(false, issue.Trim());
    }

    private async Task<OpenPathResponse> TryOpenPathInRuntimeFileManager(string path)
    {
        try
        {
            var result = await this.http.PostAsJsonAsync("/open/path", new OpenPathRequest(path), this.jsonRustSerializerOptions);
            if (!result.IsSuccessStatusCode)
            {
                this.logger!.LogWarning("Failed to open a path in the file manager through the Rust runtime: '{StatusCode}'", result.StatusCode);
                return new OpenPathResponse(false, string.Format(TB("The runtime file manager endpoint returned '{0}'."), result.StatusCode));
            }

            var response = await result.Content.ReadFromJsonAsync<OpenPathResponse>(this.jsonRustSerializerOptions);
            return response.Success
                ? response
                : new OpenPathResponse(false, string.IsNullOrWhiteSpace(response.Issue) ? TB("The runtime file manager endpoint failed without details.") : response.Issue);
        }
        catch (Exception e)
        {
            this.logger!.LogWarning(e, "Failed to open a path in the file manager through the Rust runtime.");
            return new OpenPathResponse(false, TB("The runtime file manager endpoint is not available."));
        }
    }

    private OpenPathResponse TryOpenPathInLocalFileManager(string path)
    {
        try
        {
            var target = ResolveFileManagerTarget(path);
            if (target is null)
                return new OpenPathResponse(false, TB("The path does not exist and its parent folder could not be found."));

            using var process = Process.Start(CreateFileManagerStartInfo(target.Value));
            return process is null
                ? new OpenPathResponse(false, TB("The local file manager command did not start."))
                : new OpenPathResponse(true, string.Empty);
        }
        catch (Exception e)
        {
            this.logger!.LogWarning(e, "Failed to open a path in the local file manager.");
            return new OpenPathResponse(false, string.Format(TB("The local file manager command failed: {0}"), e.Message));
        }
    }

    private static FileManagerTarget? ResolveFileManagerTarget(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var requestedPath = path.Trim();
        if (File.Exists(requestedPath))
            return new FileManagerTarget(requestedPath, true);

        if (Directory.Exists(requestedPath))
            return new FileManagerTarget(requestedPath, false);

        var parent = Directory.GetParent(requestedPath)?.FullName;
        return !string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent)
            ? new FileManagerTarget(parent, false)
            : null;
    }

    private static ProcessStartInfo CreateFileManagerStartInfo(FileManagerTarget target)
    {
        if (OperatingSystem.IsWindows())
        {
            var windowsInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
            windowsInfo.ArgumentList.Add(target.RevealFile ? $"/select,{target.Path}" : target.Path);
            return windowsInfo;
        }

        if (OperatingSystem.IsMacOS())
        {
            var macOsInfo = new ProcessStartInfo("open") { UseShellExecute = false };
            if (target.RevealFile)
                macOsInfo.ArgumentList.Add("-R");

            macOsInfo.ArgumentList.Add(target.Path);
            return macOsInfo;
        }

        var linuxInfo = new ProcessStartInfo("xdg-open") { UseShellExecute = false };
        var directory = target.RevealFile ? Path.GetDirectoryName(target.Path) ?? target.Path : target.Path;
        linuxInfo.ArgumentList.Add(directory);
        return linuxInfo;
    }

    private readonly record struct FileManagerTarget(string Path, bool RevealFile);
}
