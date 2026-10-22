namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// Lua to copy, or an explanation of why the export failed. An empty successful export has nothing to copy.
/// </summary>
public sealed record ToolSettingsExportResult(string LuaCode = "", string ErrorMessage = "")
{
    public bool Success => string.IsNullOrEmpty(this.ErrorMessage);
}