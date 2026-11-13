using AIStudio.Tools;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks which answer the message bus returns when it asks its receivers a question.
/// </summary>
/// <remarks>
/// Before the user leaves a chat or opens another one, AI Studio asks the chat whether it has unsaved
/// changes. Every component which never narrows down its events receives that question as well — one
/// for each block of each message in the chat, among others. Such a component has nothing to say about
/// it and must not drown out the one which has. Nor must the chat of another window: AI Studio keeps the
/// circuits of reloaded windows around, and their chats still listen. The bus walks its receivers in the
/// order of their hash codes, so the fakes here pin their hash codes, and each test checks both orders.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class MessageBusTests
{
    private readonly List<IMessageBusReceiver> registeredReceivers = [];

    [TearDown]
    public void UnregisterReceivers()
    {
        //
        // The bus is static state of the whole application, which is why this fixture does not run
        // alongside others, and why every receiver has to leave it again.
        //
        foreach (var receiver in this.registeredReceivers)
            MessageBus.INSTANCE.Unregister(receiver);

        this.registeredReceivers.Clear();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task AReceiverWithoutAnAnswerDoesNotDrownOutTheOneWithAnAnswer(bool isSilentReceiverAskedFirst)
    {
        this.Register(new SilentReceiver(isSilentReceiverAskedFirst ? 0 : 1));
        this.Register(new AnsweringReceiver(isSilentReceiverAskedFirst ? 1 : 0, true), Event.HAS_CHAT_UNSAVED_CHANGES);

        var hasUnsavedChanges = await MessageBus.INSTANCE.SendMessageUseFirstResult<bool, bool>(null, Event.HAS_CHAT_UNSAVED_CHANGES);

        Assert.That(hasUnsavedChanges, Is.True, "Only the chat knows about its unsaved changes; a component which does not handle the question has nothing to say about them.");
    }

    [Test]
    public async Task AQuestionNobodyAnswersYieldsTheDefault()
    {
        this.Register(new SilentReceiver(0));
        this.Register(new SilentReceiver(1));

        var hasUnsavedChanges = await MessageBus.INSTANCE.SendMessageUseFirstResult<bool, bool>(null, Event.HAS_CHAT_UNSAVED_CHANGES);

        Assert.That(hasUnsavedChanges, Is.False, "Without a chat to answer, nothing is unsaved.");
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task AQuestionIsAnsweredInTheWindowWhichAsksIt(bool isOtherWindowAskedFirst)
    {
        var askingWindow = new CircuitStateService();
        var otherWindow = new CircuitStateService();
        var askingComponent = new AskingComponent();
        this.RegisterInCircuit(askingComponent, askingWindow);
        this.RegisterInCircuit(new AnsweringReceiver(isOtherWindowAskedFirst ? 1 : 0, false), askingWindow, Event.HAS_CHAT_UNSAVED_CHANGES);
        this.RegisterInCircuit(new AnsweringReceiver(isOtherWindowAskedFirst ? 0 : 1, true), otherWindow, Event.HAS_CHAT_UNSAVED_CHANGES);

        var hasUnsavedChanges = await MessageBus.INSTANCE.SendMessageUseFirstResult<bool, bool>(askingComponent, Event.HAS_CHAT_UNSAVED_CHANGES);

        Assert.That(hasUnsavedChanges, Is.False, "The chat of another window, such as one which was reloaded, knows nothing about the chat the user is looking at.");
    }

    [Test]
    public async Task AReceiverWithoutAWindowAnswersEveryWindow()
    {
        var askingWindow = new CircuitStateService();
        var askingComponent = new AskingComponent();
        this.RegisterInCircuit(askingComponent, askingWindow);
        this.Register(new AnsweringReceiver(0, true), Event.HAS_CHAT_UNSAVED_CHANGES);

        var hasUnsavedChanges = await MessageBus.INSTANCE.SendMessageUseFirstResult<bool, bool>(askingComponent, Event.HAS_CHAT_UNSAVED_CHANGES);

        Assert.That(hasUnsavedChanges, Is.True,"Services such as the update service live longer than any window, so they serve all of them.");
    }

    private void Register(IMessageBusReceiver receiver, params Event[] events) => this.RegisterInCircuit(receiver, null, events);

    private void RegisterInCircuit(IMessageBusReceiver receiver, CircuitStateService? circuitState, params Event[] events)
    {
        MessageBus.INSTANCE.RegisterComponent(receiver, circuitState);
        if (events.Length > 0)
            MessageBus.INSTANCE.ApplyFilters(receiver, [], events.ToHashSet());

        this.registeredReceivers.Add(receiver);
    }

    /// <summary>
    /// Asks the question, like the workspace list or the layout do. It is a receiver itself, which is how
    /// the bus knows its window.
    /// </summary>
    private sealed class AskingComponent : ComponentBase, IMessageBusReceiver
    {
        public Task ProcessMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) => Task.CompletedTask;

        public Task<MessageBusAnswer<TResult>> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data) => Task.FromResult(MessageBusAnswer<TResult>.NONE);
    }

    /// <summary>
    /// Behaves like every component which never narrows down its events: it receives every question,
    /// but handles none of them.
    /// </summary>
    /// <param name="hashCode">Decides when the bus asks this receiver, see the remarks on the fixture.</param>
    private sealed class SilentReceiver(int hashCode) : IMessageBusReceiver
    {
        public Task ProcessMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) => Task.CompletedTask;

        public Task<MessageBusAnswer<TResult>> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data) => Task.FromResult(MessageBusAnswer<TResult>.NONE);

        public override int GetHashCode() => hashCode;
    }

    /// <summary>
    /// Behaves like the chat: it handles the question and answers it.
    /// </summary>
    /// <param name="hashCode">Decides when the bus asks this receiver, see the remarks on the fixture.</param>
    /// <param name="answer">The answer to every question.</param>
    private sealed class AnsweringReceiver(int hashCode, bool answer) : IMessageBusReceiver
    {
        public Task ProcessMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) => Task.CompletedTask;

        public Task<MessageBusAnswer<TResult>> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data) => Task.FromResult(new MessageBusAnswer<TResult>(true, (TResult)(object)answer));

        public override int GetHashCode() => hashCode;
    }
}