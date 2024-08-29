namespace AIStudio.Tools.Rust;

public readonly record struct GetSecretRequest(
    string Destination,
    string UserName
);