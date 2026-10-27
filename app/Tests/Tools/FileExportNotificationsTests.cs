using AIStudio.Tools;

using Microsoft.AspNetCore.Components;

using MudBlazor;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks that the notification that a file export runs never appears in a window after the export ended.
/// </summary>
/// <remarks>
/// The notifications of an export are sent in order, but they may take effect out of it: the one that
/// the export runs comes from a timer and waits in the queue of the renderer, while the end usually
/// comes from the renderer itself and takes effect at once.
/// </remarks>
[TestFixture]
public sealed class FileExportNotificationsTests
{
    private const string KEY = "export";
    private const string FILE_PATH = "/home/user/Documents/report.docx";

    private static readonly DataFileExportRunningMessage RUNNING = new(KEY, FILE_PATH);
    private static readonly DataFileExportSuccessMessage SUCCESS = new(KEY, "icon", "The export succeeded.", FILE_PATH, true);

    [Test]
    public void TheSuccessReplacesTheRunningNotification()
    {
        var snackbar = new RecordingSnackbar();
        var notifications = new FileExportNotifications(snackbar);

        notifications.ShowRunning(RUNNING);
        notifications.ShowSuccess(SUCCESS, false);

        Assert.That(snackbar.Calls, Is.EqualTo(new[] { $"add FileExportRunningMessage {KEY}", $"remove {KEY}", $"add FileExportSuccessMessage {KEY}" }));
    }

    [Test]
    public void ARunningNotificationWhichComesAfterTheSuccessIsDropped()
    {
        var snackbar = new RecordingSnackbar();
        var notifications = new FileExportNotifications(snackbar);

        notifications.ShowSuccess(SUCCESS, false);
        notifications.ShowRunning(RUNNING);

        Assert.That(snackbar.Calls, Is.EqualTo(new[] { $"remove {KEY}", $"add FileExportSuccessMessage {KEY}" }));
    }

    [Test]
    public void ARunningNotificationWhichComesAfterItsRemovalIsDropped()
    {
        var snackbar = new RecordingSnackbar();
        var notifications = new FileExportNotifications(snackbar);

        notifications.HideRunning(RUNNING);
        notifications.ShowRunning(RUNNING);

        Assert.That(snackbar.Calls, Is.EqualTo(new[] { $"remove {KEY}" }));
    }

    [Test]
    public void TheEndOfOneExportDoesNotSilenceAnother()
    {
        var snackbar = new RecordingSnackbar();
        var notifications = new FileExportNotifications(snackbar);

        notifications.ShowSuccess(SUCCESS, false);
        notifications.ShowRunning(RUNNING with { Key = "another export" });

        Assert.That(snackbar.Calls, Does.Contain("add FileExportRunningMessage another export"));
    }

    [TestCase(false, ExpectedResult = true)]
    [TestCase(true, ExpectedResult = false)]
    public bool TheSuccessWaitsForTheUserUnlessTheyChoseOtherwise(bool closesAutomatically)
    {
        var snackbar = new RecordingSnackbar();
        new FileExportNotifications(snackbar).ShowSuccess(SUCCESS, closesAutomatically);

        return snackbar.RequiresInteraction.Single();
    }

    /// <summary>
    /// Records which notifications are added and removed, by their key, and whether a component
    /// notification waits for the user.
    /// </summary>
    private sealed class RecordingSnackbar : ISnackbar
    {
        public List<string> Calls { get; } = [];

        public List<bool> RequiresInteraction { get; } = [];

        public SnackbarConfiguration Configuration { get; } = new();

        public IEnumerable<Snackbar> ShownSnackbars => [];

        public event Action? OnSnackbarsUpdated
        {
            add { }
            remove { }
        }

        public Snackbar? Add(string message, Severity severity = Severity.Normal, Action<SnackbarOptions>? configure = null, string? key = null) => this.Record("string", key);

        public Snackbar? Add(MarkupString message, Severity severity = Severity.Normal, Action<SnackbarOptions>? configure = null, string? key = null) => this.Record(nameof(MarkupString), key);

        public Snackbar? Add(RenderFragment message, Severity severity = Severity.Normal, Action<SnackbarOptions>? configure = null, string? key = null) => this.Record(nameof(RenderFragment), key);

        public Snackbar? Add<T>(Dictionary<string, object>? componentParameters = null, Severity severity = Severity.Normal, Action<SnackbarOptions>? configure = null, string? key = null) where T : IComponent
        {
            var options = new SnackbarOptions(severity, this.Configuration);
            configure?.Invoke(options);
            this.RequiresInteraction.Add(options.RequireInteraction);

            return this.Record(typeof(T).Name, key);
        }

        public void Clear() => this.Calls.Add("clear");

        public void Remove(Snackbar snackbar) => this.Calls.Add("remove one");

        public void RemoveByKey(string key) => this.Calls.Add($"remove {key}");

        public void Dispose()
        {
        }

        private Snackbar? Record(string content, string? key)
        {
            this.Calls.Add($"add {content} {key}");
            return null;
        }
    }
}