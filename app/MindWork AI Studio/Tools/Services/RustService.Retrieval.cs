namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    public async Task<string> GetPDFText(string filePath)
    {
        var response = await this.http.GetAsync($"/retrieval/fs/read/pdf?file_path={filePath}");
        if (!response.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to read the PDF file due to an network error: '{response.StatusCode}'");
            return string.Empty;
        }

        return await response.Content.ReadAsStringAsync();
    }
}