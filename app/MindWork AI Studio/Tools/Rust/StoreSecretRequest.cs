namespace AIStudio.Tools.Rust;

public readonly record struct StoreSecretRequest(string Destination, string UserName, EncryptedText Secret);