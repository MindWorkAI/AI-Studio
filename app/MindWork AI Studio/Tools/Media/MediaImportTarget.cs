namespace AIStudio.Tools.Media;

/// <summary>Identifies the concrete attachment or file-content field inside an owner.</summary>
public readonly record struct MediaImportTarget(MediaImportOwner Owner, string TargetId);