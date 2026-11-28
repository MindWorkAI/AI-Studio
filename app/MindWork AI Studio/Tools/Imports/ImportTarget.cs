namespace AIStudio.Tools.Imports;

/// <summary>Identifies the concrete field inside an owner which an import fills, e.g., an attachment list or a file-content field.</summary>
public readonly record struct ImportTarget(ImportOwner Owner, string TargetId);