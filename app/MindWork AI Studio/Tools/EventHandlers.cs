using Microsoft.AspNetCore.Components;

namespace AIStudio.Tools;

/// <summary>
/// Add handling for more DOM events to Blazor components.
/// </summary>
/// <remarks>
/// See https://learn.microsoft.com/en-us/aspnet/core/blazor/components/event-handling. It is important
/// that this class is named EventHandlers.
/// </remarks>
[EventHandler("onmouseenter", typeof(EventArgs), enableStopPropagation: true, enablePreventDefault: true)]
[EventHandler("onmouseleave", typeof(EventArgs), enableStopPropagation: true, enablePreventDefault: true)]
public static class EventHandlers;