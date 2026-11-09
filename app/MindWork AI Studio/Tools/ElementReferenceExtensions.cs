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

        /// <summary>
        /// Scrolls back to the sticky header right after this empty anchor, so that the content below the header
        /// starts at the top again. Nothing moves while the header sits in its place anyway.
        /// </summary>
        /// <param name="jsRuntime">The JavaScript runtime of the page.</param>
        public async ValueTask ScrollToStickyHeaderAsync(IJSRuntime jsRuntime)
        {
            await jsRuntime.InvokeVoidAsync("scrollToStickyHeader", elementReference);
        }
    }
}