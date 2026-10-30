using System.Collections.Concurrent;

using AIStudio.Chat;
using AIStudio.Tools;
using AIStudio.Tools.AssistantSessions;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Tests.Tools.AssistantSessions;

/// <summary>
/// Checks that an assistant which waits for a tool call to be confirmed says so, also where the user does not look.
/// </summary>
/// <remarks>
/// An assistant runs on when the user leaves it. A confirmation which waits there unnoticed would hold
/// the run forever, so the session carries the state for the card and the navigation, and an assistant
/// nobody shows notifies the user. One which the user looks at must not: the request is right in front
/// of them.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class AssistantSessionToolApprovalTests
{
    private static readonly AssistantSessionKey KEY = new(AIStudio.Tools.Components.TRANSLATION_ASSISTANT, nameof(AssistantSessionToolApprovalTests));

    private readonly NotificationRecorder notifications = new();

    private string title = string.Empty;

    [SetUp]
    public void ListenForNotifications()
    {
        // A title of its own, so the recorder ignores what other parts of the app send meanwhile:
        this.title = $"Translation {Guid.NewGuid():N}";
        this.notifications.Messages.Clear();
        this.notifications.StayedUntilClosed.Clear();
        this.notifications.Keys.Clear();
        this.notifications.HiddenKeys.Clear();

        //
        // The bus is static state of the whole application, which is why this fixture does not run
        // alongside others, and why the recorder leaves it again after every test.
        //
        MessageBus.INSTANCE.RegisterComponent(this.notifications);
        MessageBus.INSTANCE.ApplyFilters(this.notifications, [], [Event.SHOW_WARNING, Event.HIDE_NOTIFICATION]);
    }

    [TearDown]
    public void StopListening() => MessageBus.INSTANCE.Unregister(this.notifications);

    [Test]
    public async Task AWaitingAssistantSaysSoUntilTheUserAnswered()
    {
        var (service, thread) = await this.StartAsync();

        await service.SetWaitingForToolApprovalAsync(thread.ChatId, true);
        var waiting = service.TryGetSnapshot(KEY);
        var anyWaiting = service.IsAnySessionWaitingForToolApproval;

        await service.SetWaitingForToolApprovalAsync(thread.ChatId, false);

        Assert.Multiple(() =>
        {
            Assert.That(waiting?.IsWaitingForToolApproval, Is.True, "The card of the assistant shows the warning from the snapshot.");
            Assert.That(anyWaiting, Is.True, "The navigation shows the warning while any assistant waits.");
            Assert.That(service.TryGetSnapshot(KEY)?.IsWaitingForToolApproval, Is.False, "Once the user answered, nothing waits anymore.");
            Assert.That(service.IsAnySessionWaitingForToolApproval, Is.False);
        });
    }

    [Test]
    public async Task AnAssistantNobodyShowsNotifiesTheUser()
    {
        var (service, thread) = await this.StartAsync();

        await service.SetWaitingForToolApprovalAsync(thread.ChatId, true);

        Assert.That(this.Notifications().Count(message => message.Contains("waits for you to confirm a tool call", StringComparison.Ordinal)), Is.EqualTo(1), "The user works elsewhere and would never learn that the assistant waits.");
    }

    [Test]
    public async Task TheNotificationStaysUntilTheUserClosesItAndQuotesALongTitleShortened()
    {
        this.title = $"{this.title} {new string('x', 200)}";
        var (service, thread) = await this.StartAsync();

        await service.SetWaitingForToolApprovalAsync(thread.ChatId, true);

        var message = this.notifications.Messages.Single(message => message.Contains("waits for you to confirm a tool call", StringComparison.Ordinal));
        Assert.Multiple(() =>
        {
            Assert.That(this.notifications.StayedUntilClosed.Single(), Is.True, "The assistant waits until the user answers, so the notification must not disappear before.");
            Assert.That(message, Does.Not.Contain(this.title), "A title may be as long as a whole prompt, which no notification should repeat.");
            Assert.That(message, Does.Contain("..."), "The cut is marked, so the user does not take it for the whole title.");
        });
    }

    [Test]
    public async Task TheNotificationGoesOnceTheUserAnswered()
    {
        var (service, thread) = await this.StartAsync();
        await service.SetWaitingForToolApprovalAsync(thread.ChatId, true);

        await service.SetWaitingForToolApprovalAsync(thread.ChatId, false);

        Assert.That(this.notifications.HiddenKeys, Does.Contain(this.notifications.Keys.Single()), "Nothing waits anymore, so a notification left standing would only pile up.");
    }

    [Test]
    public async Task TheNotificationGoesWhenTheUserComesBack()
    {
        var (service, thread) = await this.StartAsync();
        await service.SetWaitingForToolApprovalAsync(thread.ChatId, true);

        service.SetShown(KEY, true);

        Assert.That(() => this.notifications.HiddenKeys.Contains(this.notifications.Keys.Single()), Is.True.After(2000, 20), "The request stands in front of the user again.");
    }

    [Test]
    public async Task TheNotificationGoesWhenTheRunEnds()
    {
        var (service, thread) = await this.StartAsync();
        await service.SetWaitingForToolApprovalAsync(thread.ChatId, true);
        var sessionId = service.TryGetSnapshot(KEY)!.SessionId;

        await service.CompleteAsync(KEY, sessionId, AssistantSessionStatus.CANCELED, string.Empty, thread, []);

        Assert.That(this.notifications.HiddenKeys, Does.Contain(this.notifications.Keys.Single()), "A stopped run waits for nothing.");
    }

    [Test]
    public async Task AnAssistantTheUserLooksAtDoesNotNotify()
    {
        var (service, thread) = await this.StartAsync();
        service.SetShown(KEY, true);

        await service.SetWaitingForToolApprovalAsync(thread.ChatId, true);

        Assert.That(this.Notifications(), Is.Empty, "The request stands right in front of the user.");
    }

    [Test]
    public async Task AnAssistantWhichWasLeftNotifiesAgain()
    {
        var (service, thread) = await this.StartAsync();

        // The next page may show the assistant before the previous one is gone:
        service.SetShown(KEY, true);
        service.SetShown(KEY, true);
        service.SetShown(KEY, false);
        service.SetShown(KEY, false);

        await service.SetWaitingForToolApprovalAsync(thread.ChatId, true);

        Assert.That(this.Notifications(), Has.Count.EqualTo(1), "Both pages are gone, so nobody sees the assistant anymore.");
    }

    [Test]
    public async Task LeavingAnAssistantWhichWaitsNotifiesTheUser()
    {
        var (service, thread) = await this.StartAsync();
        service.SetShown(KEY, true);
        await service.SetWaitingForToolApprovalAsync(thread.ChatId, true);

        // The request appeared in front of the user, who then went elsewhere without answering:
        service.SetShown(KEY, false);

        Assert.That(() => this.Notifications().Count, Is.EqualTo(1).After(2000, 20), "From now on, only the notification tells the user that the assistant waits.");
    }

    [Test]
    public async Task AFinishedRunWaitsNoMore()
    {
        var (service, thread) = await this.StartAsync();
        await service.SetWaitingForToolApprovalAsync(thread.ChatId, true);
        var sessionId = service.TryGetSnapshot(KEY)!.SessionId;

        // Stopping the request ends the wait in the loop as well, but the session must not rely on that:
        await service.CompleteAsync(KEY, sessionId, AssistantSessionStatus.CANCELED, string.Empty, thread, []);

        Assert.Multiple(() =>
        {
            Assert.That(service.TryGetSnapshot(KEY)?.IsWaitingForToolApproval, Is.False);
            Assert.That(service.IsAnySessionWaitingForToolApproval, Is.False, "A warning left behind in the navigation would point to nothing.");
        });
    }

    [Test]
    public async Task AThreadOfNoAssistantIsIgnored()
    {
        var (service, _) = await this.StartAsync();

        // Chats run the same loop, and so do the threads an assistant runs out of sight:
        await service.SetWaitingForToolApprovalAsync(Guid.NewGuid(), true);

        Assert.Multiple(() =>
        {
            Assert.That(service.IsAnySessionWaitingForToolApproval, Is.False);
            Assert.That(this.Notifications(), Is.Empty);
        });
    }

    private async Task<(AssistantSessionService Service, ChatThread Thread)> StartAsync()
    {
        var service = new AssistantSessionService(MessageBus.INSTANCE);
        var thread = new ChatThread { ChatId = Guid.NewGuid() };
        await service.TryBeginAsync(KEY, this.title, new CancellationTokenSource(), thread, []);
        return (service, thread);
    }

    private List<string> Notifications() => this.notifications.Messages
        .Where(message => message.Contains(this.title, StringComparison.Ordinal))
        .ToList();

    /// <summary>
    /// Keeps the texts of the notifications, as the layout would show them.
    /// </summary>
    private sealed class NotificationRecorder : IMessageBusReceiver
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public ConcurrentQueue<bool> StayedUntilClosed { get; } = new();

        public ConcurrentQueue<string> Keys { get; } = new();

        public ConcurrentQueue<string> HiddenKeys { get; } = new();

        public Task ProcessMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data)
        {
            if (triggeredEvent is Event.SHOW_WARNING && data is DataWarningMessage warning)
            {
                this.Messages.Enqueue(warning.Message);
                this.StayedUntilClosed.Enqueue(warning.StaysUntilClosed);
                if (warning.Key is not null)
                    this.Keys.Enqueue(warning.Key);
            }

            if (triggeredEvent is Event.HIDE_NOTIFICATION && data is string key)
                this.HiddenKeys.Enqueue(key);

            return Task.CompletedTask;
        }

        public Task<MessageBusAnswer<TResult>> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data) => Task.FromResult(MessageBusAnswer<TResult>.NONE);
    }
}