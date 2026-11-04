using Microsoft.AspNetCore.Components;

namespace AIStudio.Tools;

public static class ElementReferenceExtensions
{
    extension(ElementReference elementReference)
    {
        public async ValueTask ScrollIntoViewAsync(IJSRuntime jsRuntime)
        {
            await jsRuntime.InvokeVoidAsync("scrollToBottom", elementReference);
        }
    }
}