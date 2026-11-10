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

        /// <summary>
        /// Keeps the height of this sticky header, and the space it leaves free below, in the CSS variables
        /// <c>--sticky-header-height</c> and <c>--sticky-space-below-header</c> of its scrolling container, so that
        /// content further down can stick right below it.
        /// </summary>
        /// <remarks>
        /// The variables follow every change of size, e.g., when a narrower window wraps the header into more lines.
        /// Call it again for a new element only: a header which left the page stops being followed by itself.
        /// </remarks>
        /// <param name="jsRuntime">The JavaScript runtime of the page.</param>
        public async ValueTask FollowStickyHeaderAsync(IJSRuntime jsRuntime)
        {
            await jsRuntime.InvokeVoidAsync("followStickyHeader", elementReference);
        }
    }
}