namespace AIStudio.Tools.DataBackup;

/// <summary>
/// A secret setting of a tool which the user set up, and which may lack its value after a restore.
/// </summary>
/// <param name="ToolId">The tool.</param>
/// <param name="FieldName">The setting, as the schema of the tool names it.</param>
/// <param name="ToolName">The name of the tool, as the user reads it.</param>
/// <param name="SettingLabel">The label of the setting, as the user reads it.</param>
public sealed record DataRestoreToolSecret(string ToolId, string FieldName, string ToolName, string SettingLabel);