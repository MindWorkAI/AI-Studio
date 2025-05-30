namespace AIStudio.Tools;

public readonly record struct PandocInstallation(bool CheckWasSuccessful, string ErrorMessage, bool IsAvailable, string Version, bool IsLocalInstallation);