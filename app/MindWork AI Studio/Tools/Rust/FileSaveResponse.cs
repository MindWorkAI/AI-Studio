namespace AIStudio.Tools.Rust;

public readonly record struct FileSaveResponse(bool UserCancelled, string SaveFilePath);
