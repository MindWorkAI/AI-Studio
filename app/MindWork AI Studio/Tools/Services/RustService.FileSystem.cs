using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    public async Task<DirectorySelectionResponse> SelectDirectory(string title, string? initialDirectory = null)
    {
        PreviousDirectory? previousDirectory = initialDirectory is null ? null : new (initialDirectory);
        var result = await this.http.PostAsJsonAsync($"/select/directory?title={title}", previousDirectory, this.jsonRustSerializerOptions);
        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to select a directory: '{result.StatusCode}'");
            return new DirectorySelectionResponse(true, string.Empty);
        }
        
        return await result.Content.ReadFromJsonAsync<DirectorySelectionResponse>(this.jsonRustSerializerOptions);
    }
    
    public async Task<FileSelectionResponse> SelectFile(string title, FileTypeFilter? filter = null, string? initialFile = null)
    {
        var payload = new SelectFileOptions
        {
            Title = title,
            PreviousFile = initialFile is null ? null : new (initialFile),
            Filter = filter
        };

        var result = await this.http.PostAsJsonAsync("/select/file", payload, this.jsonRustSerializerOptions);
        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to select a file: '{result.StatusCode}'");
            return new FileSelectionResponse(true, string.Empty);
        }

        return await result.Content.ReadFromJsonAsync<FileSelectionResponse>(this.jsonRustSerializerOptions);
    }

    public async Task<FilesSelectionResponse> SelectFiles(string title, FileTypeFilter? filter = null, string? initialFile = null)
    {
        var payload = new SelectFileOptions
        {
            Title = title,
            PreviousFile = initialFile is null ? null : new (initialFile),
            Filter = filter
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
    /// <param name="filter">An optional file type filter for filtering specific file formats.</param>
    /// <param name="initialFile">An optional initial file path to pre-fill in the dialog.</param>
    /// <returns>A <see cref="FileSaveResponse"/> object containing information about whether the user canceled the
    /// operation and whether the select operation was successful.</returns>
    public async Task<FileSaveResponse> SaveFile(string title, FileTypeFilter? filter = null, string? initialFile = null)
    {
        var payload = new SaveFileOptions
        {
            Title = title,
            PreviousFile = initialFile is null ? null : new (initialFile),
            Filter = filter
        };
        
        var result = await this.http.PostAsJsonAsync("/save/file", payload, this.jsonRustSerializerOptions);
        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to select a file for writing operation '{result.StatusCode}'");
            return new FileSaveResponse(true, string.Empty);
        }
        
        return await result.Content.ReadFromJsonAsync<FileSaveResponse>(this.jsonRustSerializerOptions);
    }
}