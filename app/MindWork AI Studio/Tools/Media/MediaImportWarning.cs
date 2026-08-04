namespace AIStudio.Tools.Media;

/// <summary>One user-visible media warning retained until its owner is displayed.</summary>
public sealed record MediaImportWarning(string FileName, string UserMessage);