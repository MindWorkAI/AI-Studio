using System.Reflection;

using AIStudio.Assistants;
using AIStudio.Assistants.VisualBriefing;
using AIStudio.Tools;
using AIStudio.Tools.AssistantLaunch;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Tests.Assistants;

/// <summary>
/// Checks that every assistant which content may be sent to takes it when it opens.
/// </summary>
/// <remarks>
/// The text waits on the message bus until its destination opens, and the bus hands it only to a receiver which
/// asks for its type: a receiver which asks for another type, or none at all, gets nothing, and the text is lost
/// without a word. That is why each destination is looked up by its route and checked for its part of the
/// receiving. The Visual Briefing assistant is no assistant of the common kind and takes the text itself. The
/// components are written out in full, since a short name would find a namespace of this assembly first.
/// </remarks>
[TestFixture]
public sealed class SendToReceiverTests
{
    private static readonly Assembly APP = typeof(AssistantLaunchService).Assembly;

    private static IEnumerable<AIStudio.Tools.Components> Destinations() => Enum.GetValues<AIStudio.Tools.Components>()
        .Where(component => component.AllowSendTo())
        .Where(component => component is not AIStudio.Tools.Components.CHAT);

    [TestCaseSource(nameof(Destinations))]
    public void ADestinationHasAnEventAndAPage(AIStudio.Tools.Components destination)
    {
        var data = destination.GetData();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data.Event, Is.Not.EqualTo(Event.NONE), "Without an event of its own, the text has nothing to travel with.");
            Assert.That(PagesOf(data.Route), Has.Count.EqualTo(1), "The text is taken by the page which opens under the route.");
        }
    }

    [TestCaseSource(nameof(Destinations))]
    public void ADestinationTakesTheTextSentToIt(AIStudio.Tools.Components destination)
    {
        var page = PagesOf(destination.GetData().Route).Single();
        if (page == typeof(VisualBriefingAssistant))
            Assert.Pass("The Visual Briefing assistant takes the text itself, since it is no assistant of the common kind.");

        var applyMethod = page.GetMethod("ApplyTextLaunchPayload", BindingFlags.Instance | BindingFlags.NonPublic);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(IsAssistant(page), Is.True, $"{page.Name} is expected to be an assistant.");
            Assert.That(applyMethod, Is.Not.Null);
            Assert.That(applyMethod!.DeclaringType, Is.Not.EqualTo(applyMethod.GetBaseDefinition().DeclaringType), $"{page.Name} allows sending to it, so it has to put the text sent to it into a field.");
        }
    }

    [Test]
    public void TextForTheChatGoesIntoTheFieldForTheNextMessage()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(AssistantLaunchService.TextEventFor(AIStudio.Tools.Components.CHAT), Is.EqualTo(Event.SEND_TO_CHAT_INPUT));
            Assert.That(AssistantLaunchService.TextEventFor(AIStudio.Tools.Components.SLIDE_BUILDER_ASSISTANT), Is.EqualTo(Event.SEND_TO_SLIDE_BUILDER_ASSISTANT));
        }
    }

    private static List<Type> PagesOf(string route) => APP.GetTypes()
        .Where(type => type.GetCustomAttributes<RouteAttribute>().Any(attribute => string.Equals(attribute.Template, route, StringComparison.Ordinal)))
        .ToList();

    private static bool IsAssistant(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(AssistantBase<>))
                return true;

        return false;
    }
}
