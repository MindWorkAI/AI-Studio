namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    public async Task<string> ReadUserLanguage(bool forceRequest = false)
    {
        if (!forceRequest && !string.IsNullOrWhiteSpace(this.cachedUserLanguage))
            return this.cachedUserLanguage;

        await this.userLanguageLock.WaitAsync();
        try
        {
            if (!forceRequest && !string.IsNullOrWhiteSpace(this.cachedUserLanguage))
                return this.cachedUserLanguage;

            var response = await this.http.GetAsync("/system/language");
            if (!response.IsSuccessStatusCode)
            {
                this.logger!.LogError($"Failed to read the user language from Rust: '{response.StatusCode}'");
                return string.Empty;
            }

            var userLanguage = (await response.Content.ReadAsStringAsync()).Trim();
            if (string.IsNullOrWhiteSpace(userLanguage))
                return string.Empty;
            
            this.cachedUserLanguage = userLanguage;
            return userLanguage;
        }
        finally
        {
            this.userLanguageLock.Release();
        }
    }
}