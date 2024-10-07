namespace AIStudio.Tools.Rust;

public readonly record struct SelectSecretRequest(string Destination, string UserName, bool IsTrying);