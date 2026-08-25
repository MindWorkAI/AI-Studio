using AIStudio.Assistants;

namespace AIStudio.Tools;

public static class JsRuntimeExtensions
{
    private static readonly ILogger LOGGER = Program.LOGGER_FACTORY.CreateLogger(nameof(JsRuntimeExtensions));

    public static async Task GenerateAndShowDiff(this IJSRuntime jsRuntime, string text1, string text2)
    {
        await jsRuntime.InvokeVoidAsync("generateDiff", text1, text2, AssistantLowerBase.RESULT_DIV_ID, AssistantLowerBase.BEFORE_RESULT_DIV_ID);
    }

    public static async Task ClearDiv(this IJSRuntime jsRuntime, string divId)
    {
        await jsRuntime.InvokeVoidAsync("clearDiv", divId);
    }

    /// <summary>
    /// Calls a JavaScript function which returns nothing, and tolerates a circuit which is already gone.
    /// </summary>
    /// <remarks>
    /// Blazor cannot issue JS interop calls once the browser connection of a circuit is gone. That happens
    /// during every reload and while a component gets disposed, so the failure is expected rather than
    /// exceptional. Discarding such a call is not an option, though: the discarded task keeps the fault
    /// until the finalizer reports it as an unobserved task exception, without any hint at its origin.
    /// This method is the one place which knows how to await such a call and what to do with its failure.
    /// </remarks>
    /// <param name="jsRuntime">The JS runtime to call.</param>
    /// <param name="identifier">The name of the JavaScript function.</param>
    /// <param name="args">The arguments for the JavaScript function.</param>
    public static async ValueTask TryInvokeVoidAsync(this IJSRuntime jsRuntime, string identifier, params object?[]? args)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync(identifier, args);
        }
        catch (Exception exception)
        {
            LogInvocationFailure(exception, identifier);
        }
    }

    /// <summary>
    /// Calls a function of a JavaScript module which returns nothing, and tolerates a circuit which is
    /// already gone. See the remarks on the JS runtime variant of this method.
    /// </summary>
    /// <param name="module">The JavaScript module to call.</param>
    /// <param name="identifier">The name of the function inside the module.</param>
    /// <param name="args">The arguments for the function.</param>
    public static async ValueTask TryInvokeVoidAsync(this IJSObjectReference module, string identifier, params object?[]? args)
    {
        try
        {
            await module.InvokeVoidAsync(identifier, args);
        }
        catch (Exception exception)
        {
            LogInvocationFailure(exception, identifier);
        }
    }

    private static void LogInvocationFailure(Exception exception, string identifier)
    {
        switch (exception)
        {
            //
            // The circuit is disconnected or disposed, or the call was canceled while it was on its way.
            // None of this is a defect: it is what a reload, a lost connection, or a disposed component
            // looks like from here.
            //
            case JSDisconnectedException:
            case ObjectDisposedException:
            case OperationCanceledException:
                LOGGER.LogDebug("The JS call '{Identifier}' was not completed because the browser connection was gone: {Reason}", identifier, exception.Message);
                break;

            // The call reached the browser, but failed there. That is worth knowing about:
            case JSException:
                LOGGER.LogWarning(exception, "The JS call '{Identifier}' failed in the browser.", identifier);
                break;

            default:
                LOGGER.LogError(exception, "The JS call '{Identifier}' failed unexpectedly.", identifier);
                break;
        }
    }
}