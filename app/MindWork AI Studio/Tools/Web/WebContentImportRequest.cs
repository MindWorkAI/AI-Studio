namespace AIStudio.Tools.Web;

/// <summary>
/// What the web content reader of an assistant asks the WebContentImportService to load.
/// </summary>
/// <param name="Url">The address the user typed.</param>
/// <param name="CleanContent">Whether the text content cleaner, an AI task, cleans the page.</param>
/// <param name="AssistantProvider">The model of the assistant around the reader, which the cleaner falls back on, see WebContentImportService.GetCleanerProviderOptions.</param>
public sealed record WebContentImportRequest(string Url, bool CleanContent, AIStudio.Settings.Provider AssistantProvider);