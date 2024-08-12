using AIStudio.Components;

namespace AIStudio.Tools;

public static class JsRuntimeExtensions
{
    public static async Task GenerateAndShowDiff(this IJSRuntime jsRuntime, string text1, string text2)
    {
        await jsRuntime.InvokeVoidAsync("generateDiff", text1, text2, AssistantBase.ASSISTANT_RESULT_DIV_ID, AssistantBase.AFTER_RESULT_DIV_ID);
    }
}