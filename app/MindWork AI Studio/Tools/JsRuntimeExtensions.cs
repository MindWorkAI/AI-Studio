using AIStudio.Assistants;

namespace AIStudio.Tools;

public static class JsRuntimeExtensions
{
    public static async Task GenerateAndShowDiff(this IJSRuntime jsRuntime, string text1, string text2)
    {
        await jsRuntime.InvokeVoidAsync("generateDiff", text1, text2, AssistantLowerBase.RESULT_DIV_ID, AssistantLowerBase.BEFORE_RESULT_DIV_ID);
    }
    
    public static async Task ClearDiv(this IJSRuntime jsRuntime, string divId)
    {
        await jsRuntime.InvokeVoidAsync("clearDiv", divId);
    }
}