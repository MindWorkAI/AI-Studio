using System.Diagnostics;

using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    public async Task<DirectorySelectionResponse> SelectDirectory(string title, string? initialDirectory = null)
    {
        return await this.RunFileDialog(
            "select directory",
            async () =>
            {
                var encodedTitle = Uri.EscapeDataString(title);
                var result = initialDirectory is null
                    ? await this.http.PostAsync($"/select/directory?title={encodedTitle}", null)
                    : await this.http.PostAsJsonAsync($"/select/directory?title={encodedTitle}", new PreviousDirectory(initialDirectory), this.jsonRustSerializerOptions);

                if (result.IsSuccessStatusCode)
                    return await result.Content.ReadFromJsonAsync<DirectorySelectionResponse>(this.jsonRustSerializerOptions);

                this.logger!.LogError("Failed to select a directory: '{StatusCode}'", result.StatusCode);
                return new DirectorySelectionResponse(true, string.Empty);
            },
            new DirectorySelectionResponse(true, string.Empty));
    }

    private async Task<T> RunFileDialog<T>(string operation, Func<Task<T>> showDialog, T cancelledResult)
    {
        if (!await this.fileDialogLock.WaitAsync(0))
        {
            this.logger!.LogInformation("Ignored duplicate file dialog request for '{Operation}'.", operation);
            return cancelledResult;
        }

        var stopwatch = Stopwatch.StartNew();
        this.logger!.LogInformation("Opening file dialog for '{Operation}'.", operation);
        try
        {
            return await showDialog();
        }
        finally
        {
            stopwatch.Stop();
            this.fileDialogLock.Release();
            this.logger!.LogInformation("File dialog for '{Operation}' completed after {ElapsedMilliseconds} ms.", operation, stopwatch.ElapsedMilliseconds);
        }
    }
    
    public async Task<FileSelectionResponse> SelectFile(string title, FileTypeFilter[]? filter = null, string? initialFile = null)
    {
        return await this.RunFileDialog(
            "select file",
            async () =>
            {
                var payload = new SelectFileOptions
                {
                    Title = title,
                    PreviousFile = initialFile is null ? null : new (initialFile),
                    Filter = FileTypes.AsOneFileType(filter)
                };

                var result = await this.http.PostAsJsonAsync("/select/file", payload, this.jsonRustSerializerOptions);
                if (result.IsSuccessStatusCode)
                    return await result.Content.ReadFromJsonAsync<FileSelectionResponse>(this.jsonRustSerializerOptions);

                this.logger!.LogError("Failed to select a file: '{StatusCode}'", result.StatusCode);
                return new FileSelectionResponse(true, string.Empty);
            },
            new FileSelectionResponse(true, string.Empty));
    }

    public async Task<FilesSelectionResponse> SelectFiles(string title, FileTypeFilter[]? filter = null, string? initialFile = null)
    {
        return await this.RunFileDialog(
            "select files",
            async () =>
            {
                var payload = new SelectFileOptions
                {
                    Title = title,
                    PreviousFile = initialFile is null ? null : new (initialFile),
                    Filter = FileTypes.AsOneFileType(filter)
                };

                var result = await this.http.PostAsJsonAsync("/select/files", payload, this.jsonRustSerializerOptions);
                if (result.IsSuccessStatusCode)
                    return await result.Content.ReadFromJsonAsync<FilesSelectionResponse>(this.jsonRustSerializerOptions);

                this.logger!.LogError("Failed to select files: '{StatusCode}'", result.StatusCode);
                return new FilesSelectionResponse(true, Array.Empty<string>());
            },
            new FilesSelectionResponse(true, Array.Empty<string>()));
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
        return await this.RunFileDialog(
            "save file",
            async () =>
            {
                var payload = new SaveFileOptions
                {
                    Title = title,
                    PreviousFile = initialFile is null ? null : new (initialFile),
                    Filter = FileTypes.AsOneFileType(filter)
                };

                var result = await this.http.PostAsJsonAsync("/save/file", payload, this.jsonRustSerializerOptions);
                if (result.IsSuccessStatusCode)
                    return await result.Content.ReadFromJsonAsync<FileSaveResponse>(this.jsonRustSerializerOptions);

                this.logger!.LogError("Failed to select a file for writing operation: '{StatusCode}'", result.StatusCode);
                return new FileSaveResponse(true, string.Empty);
            },
            new FileSaveResponse(true, string.Empty));
    }

    public async Task<OpenPathResponse> TryOpenPathInRuntimeFileManager(string path)
    {
        HttpResponseMessage result;
        try
        {
            result = await this.http.PostAsJsonAsync("/open/path", new OpenPathRequest(path), this.jsonRustSerializerOptions);
        }
        catch (HttpRequestException e)
        {
            this.logger!.LogWarning(e, "Failed to reach the Rust runtime file manager endpoint.");
            return new OpenPathResponse(false, TB("The runtime file manager endpoint is not available."));
        }
        catch (TaskCanceledException e)
        {
            this.logger!.LogWarning(e, "Timed out while reaching the Rust runtime file manager endpoint.");
            return new OpenPathResponse(false, TB("The runtime file manager endpoint is not available."));
        }

        try
        {
            if (!result.IsSuccessStatusCode)
            {
                this.logger!.LogWarning("Failed to open a path in the file manager through the Rust runtime: '{StatusCode}'", result.StatusCode);
                return new OpenPathResponse(false, string.Format(TB("The runtime file manager endpoint returned '{0}'."), result.StatusCode));
            }

            var response = await result.Content.ReadFromJsonAsync<OpenPathResponse>(this.jsonRustSerializerOptions);
            var normalizedResponse = response.Success
                ? response
                : new OpenPathResponse(false, string.IsNullOrWhiteSpace(response.Issue) ? TB("The runtime file manager endpoint failed without details.") : response.Issue);

            return normalizedResponse;
        }
        catch (Exception e)
        {
            this.logger!.LogWarning(e, "Failed to process the Rust runtime file manager endpoint response.");
            return new OpenPathResponse(false, TB("The runtime file manager endpoint failed without details."));
        }
        finally
        {
            result.Dispose();
        }
    }

    /// <summary>
    /// Opens a document in the program the system uses for it, on the given page where possible.
    /// </summary>
    /// <remarks>
    /// The page is best effort and never decides whether this succeeded. Which programs can be
    /// told a page is the runtime's business, and it says afterwards whether it managed to.
    /// </remarks>
    /// <param name="path">The document to open.</param>
    /// <param name="pageNumber">The page to show, counted from one, or null when there is none.</param>
    /// <returns>Whether the document was opened, whether the page was applied, and what went wrong.</returns>
    public async Task<OpenDocumentResponse> TryOpenDocumentInSystemViewer(string path, int? pageNumber)
    {
        HttpResponseMessage result;
        try
        {
            result = await this.http.PostAsJsonAsync("/open/document", new OpenDocumentRequest(path, pageNumber), this.jsonRustSerializerOptions);
        }
        catch (HttpRequestException e)
        {
            this.logger!.LogWarning(e, "Failed to reach the Rust runtime document endpoint.");
            return new OpenDocumentResponse(false, false, TB("The runtime document endpoint is not available."));
        }
        catch (TaskCanceledException e)
        {
            this.logger!.LogWarning(e, "Timed out while reaching the Rust runtime document endpoint.");
            return new OpenDocumentResponse(false, false, TB("The runtime document endpoint is not available."));
        }

        try
        {
            if (!result.IsSuccessStatusCode)
            {
                this.logger!.LogWarning("Failed to open a document through the Rust runtime: '{StatusCode}'", result.StatusCode);
                return new OpenDocumentResponse(false, false, string.Format(TB("The runtime document endpoint returned '{0}'."), result.StatusCode));
            }

            var response = await result.Content.ReadFromJsonAsync<OpenDocumentResponse>(this.jsonRustSerializerOptions);
            if (response.Success)
            {
                //
                // A page which was asked for but not applied is noted here and nowhere else: the
                // document is open, and the source the user clicked names the page anyway.
                //
                if (pageNumber is > 0 && !response.PageApplied)
                    this.logger!.LogInformation("Opened a document without the requested page {PageNumber}, because the system uses a program which cannot be told one.", pageNumber);

                return response;
            }

            return new OpenDocumentResponse(false, false, string.IsNullOrWhiteSpace(response.Issue) ? TB("The runtime document endpoint failed without details.") : response.Issue);
        }
        catch (Exception e)
        {
            this.logger!.LogWarning(e, "Failed to process the Rust runtime document endpoint response.");
            return new OpenDocumentResponse(false, false, TB("The runtime document endpoint failed without details."));
        }
        finally
        {
            result.Dispose();
        }
    }
}