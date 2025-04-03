namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    public async Task<string> ReadUserLanguage()
    {
        var response = await this.http.GetAsync("/system/language");
        if (!response.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to read the user language from Rust: '{response.StatusCode}'");
            return string.Empty;
        }
        
        return await response.Content.ReadAsStringAsync();
    }
}