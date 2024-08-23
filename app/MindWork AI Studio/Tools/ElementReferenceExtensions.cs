using Microsoft.AspNetCore.Components;

namespace AIStudio.Tools;

public static class ElementReferenceExtensions
{
    public static async ValueTask ScrollIntoViewAsync(this ElementReference elementReference, IJSRuntime jsRuntime)
    {
        await jsRuntime.InvokeVoidAsync("scrollToBottom", elementReference);
    }
}