namespace AIStudio.Tools.Security;

/// <summary>
/// One user action, such as sending a message, whose filtered content is reported to the user together.
/// </summary>
/// <remarks>
/// Created through PromptInjectionGuardService.BeginAction. Content which belongs to the action carries
/// the scope down to where it is read or filtered; disposing the scope ends the action.
/// </remarks>
/// <param name="guardService">The service which reports what was filtered.</param>
/// <param name="parent">The action this scope is part of, or null when it begins an action of its own.</param>
public sealed class PromptInjectionReportingScope(PromptInjectionGuardService guardService, PromptInjectionReportingScope? parent) : IAsyncDisposable
{
    /// <summary>
    /// The action this scope is part of, or null when it begins an action of its own.
    /// </summary>
    internal PromptInjectionReportingScope? Parent => parent;

    /// <summary>
    /// What was filtered while this scope was the outermost one of its action still running.
    /// </summary>
    /// <remarks>
    /// Read and written under the lock of the guard service only.
    /// </remarks>
    internal List<PromptInjectionScanResult> PendingResults { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the action is still running.
    /// </summary>
    /// <remarks>
    /// Read and written under the lock of the guard service only.
    /// </remarks>
    internal bool IsOpen { get; set; } = true;

    public async ValueTask DisposeAsync() => await guardService.EndActionAsync(this);
}