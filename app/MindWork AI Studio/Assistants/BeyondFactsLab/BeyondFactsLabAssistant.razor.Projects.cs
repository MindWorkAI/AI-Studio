using System.Globalization;

using AIStudio.Dialogs;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Assistants.BeyondFactsLab;

public partial class BeyondFactsLabAssistant
{
    /// <summary>
    /// The preparations, the one changed last first.
    /// </summary>
    private IReadOnlyList<BflProjectEntry> projects = [];

    /// <summary>
    /// The preparation shown, or null when there is none.
    /// </summary>
    private BflProjectEntry? selectedProject;

    /// <summary>
    /// Whether a change of the preparations is running.
    /// </summary>
    private bool isBusy;

    /// <summary>
    /// Gets the culture of the active language plugin, for dates.
    /// </summary>
    private static CultureInfo Culture => Tools.PluginSystem.I18N.I.Culture;

    /// <summary>
    /// Reads the preparations again and chooses the one to show.
    /// </summary>
    /// <param name="preferredId">The preparation to show, or null to keep the one shown so far.</param>
    private async Task ReloadProjectsAsync(Guid? preferredId)
    {
        this.projects = await this.Store.ListProjectsAsync();
        this.selectedProject = BflProjectSelection.Choose(this.projects, preferredId ?? this.selectedProject?.ProjectId, this.Store.LastSelectedProjectId);
    }

    /// <summary>
    /// Shows the preparation the person selected and remembers it for the next time.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    private async Task SelectProjectAsync(Guid projectId)
    {
        if (this.selectedProject?.ProjectId == projectId)
            return;

        var project = this.projects.FirstOrDefault(candidate => candidate.ProjectId == projectId);
        if (project is null)
            return;

        this.selectedProject = project;
        await this.RunStoreActionAsync(() => this.Store.RememberSelectionAsync(projectId));
    }

    /// <summary>
    /// Asks for a name and creates a preparation.
    /// </summary>
    private async Task CreateProjectAsync()
    {
        var name = await this.AskForNameAsync(T("New preparation"), T("Name the event you want to prepare for, e.g., the title of a panel discussion."), string.Empty, T("Create"));
        if (name is null)
            return;

        await this.RunStoreActionAsync(async () =>
        {
            var project = await this.Store.CreateAsync(name);
            await this.Store.RememberSelectionAsync(project.ProjectId);
            await this.ReloadProjectsAsync(project.ProjectId);
        });
    }

    /// <summary>
    /// Asks for a new name and renames the preparation shown.
    /// </summary>
    private async Task RenameProjectAsync()
    {
        if (this.selectedProject?.Project is not { } project)
            return;

        var name = await this.AskForNameAsync(T("Rename preparation"), T("Enter a new name for this preparation."), project.Name, T("Rename"));
        if (name is null)
            return;

        await this.RunStoreActionAsync(async () =>
        {
            await this.Store.RenameAsync(project.ProjectId, name);
            await this.ReloadProjectsAsync(project.ProjectId);
        });
    }

    /// <summary>
    /// Deletes the preparation shown, after the person confirmed it.
    /// </summary>
    private async Task DeleteProjectAsync()
    {
        if (this.selectedProject is not { } project)
            return;

        var parameters = new DialogParameters<ConfirmDialog>
        {
            {
                dialog => dialog.Message,
                project.IsAvailable
                    ? string.Format(T("Permanently delete the preparation '{0}' with all of its steps, chats, and work states?"), project.Name)
                    : T("This preparation cannot be opened. Permanently delete it with all of its files?")
            },
        };

        var reference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Delete preparation permanently"), parameters, DialogOptions.FULLSCREEN);
        var result = await reference.Result;
        if (result is null || result.Canceled)
            return;

        await this.RunStoreActionAsync(async () =>
        {
            await this.Store.DeleteAsync(project.ProjectId);
            this.selectedProject = null;
            await this.ReloadProjectsAsync(null);
        });
    }

    /// <summary>
    /// Marks the event of the preparation shown as having taken place, or takes that back.
    /// </summary>
    /// <param name="happened">Whether the event has taken place.</param>
    private async Task SetHappenedAsync(bool happened)
    {
        if (this.selectedProject?.Project is not { } project)
            return;

        await this.RunStoreActionAsync(async () =>
        {
            await this.Store.SetHappenedAsync(project.ProjectId, happened ? DateTimeOffset.UtcNow : null);
            await this.ReloadProjectsAsync(project.ProjectId);
        });
    }

    /// <summary>
    /// Asks for the name of a preparation.
    /// </summary>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="message">What the dialog asks for.</param>
    /// <param name="currentName">The name to start with.</param>
    /// <param name="confirmText">The text of the button which confirms.</param>
    /// <returns>The name entered, or null when the person canceled.</returns>
    private async Task<string?> AskForNameAsync(string title, string message, string currentName, string confirmText)
    {
        var parameters = new DialogParameters<SingleInputDialog>
        {
            { dialog => dialog.Message, message },
            { dialog => dialog.InputHeaderText, T("Name of the preparation") },
            { dialog => dialog.UserInput, currentName },
            { dialog => dialog.ConfirmText, confirmText },
            { dialog => dialog.ConfirmColor, Color.Primary },
            { dialog => dialog.AllowEmptyInput, false },
            { dialog => dialog.EmptyInputErrorMessage, T("Please enter a name.") },
        };

        var reference = await this.DialogService.ShowAsync<SingleInputDialog>(title, parameters, DialogOptions.FULLSCREEN);
        var result = await reference.Result;
        if (result is null || result.Canceled || result.Data is not string name || string.IsNullOrWhiteSpace(name))
            return null;

        return name;
    }

    /// <summary>
    /// Runs a change of the preparations, and tells the person when it fails.
    /// </summary>
    /// <param name="action">The change.</param>
    private async Task RunStoreActionAsync(Func<Task> action)
    {
        this.isBusy = true;
        try
        {
            await action();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            this.Logger.LogError(exception, "A change of the preparations of the BeyondFacts Lab failed.");
            await this.MessageBus.SendError(new(Icons.Material.Filled.RecordVoiceOver, T("AI Studio could not save the change of your preparations. Please try again.")));
            await this.ReloadAfterFailureAsync();
        }
        finally
        {
            this.isBusy = false;
        }
    }

    /// <summary>
    /// Reads the preparations again after a change failed, so the page shows what is stored.
    /// </summary>
    private async Task ReloadAfterFailureAsync()
    {
        try
        {
            await this.ReloadProjectsAsync(null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.Logger.LogError(exception, "The preparations of the BeyondFacts Lab could not be read.");
        }
    }

    /// <summary>
    /// Gets the icon of a preparation in the list.
    /// </summary>
    /// <param name="project">The preparation.</param>
    private static string ProjectIcon(BflProjectEntry project) => project switch
    {
        { IsAvailable: false } => Icons.Material.Filled.WarningAmber,
        { Project.HappenedAtUtc: not null } => Icons.Material.Filled.EventAvailable,
        _ => Icons.Material.Filled.Event,
    };

    /// <summary>
    /// Gets the name of a preparation in the list, also when it cannot be read.
    /// </summary>
    /// <param name="project">The preparation.</param>
    private string ProjectDisplayName(BflProjectEntry project) => string.IsNullOrWhiteSpace(project.Name) ? T("Unavailable preparation") : project.Name;

    /// <summary>
    /// Gets the line below the name of a preparation in the list: when it changed, and whether its event has taken place.
    /// </summary>
    /// <param name="project">The preparation.</param>
    private string ProjectCaption(BflProjectEntry project)
    {
        var changed = string.Format(Culture, T("Changed on {0}"), project.ModifiedAtUtc.ToLocalTime().ToString("g", Culture));
        return project.Project?.HappenedAtUtc is null ? changed : $"{changed} · {T("The event has taken place")}";
    }

    /// <summary>
    /// Gets the short reason why a preparation cannot be opened.
    /// </summary>
    /// <param name="status">The status of the preparation.</param>
    private string ProjectStatusName(BflProjectLoadStatus status) => status switch
    {
        BflProjectLoadStatus.NEWER_VERSION => T("Requires a newer AI Studio version"),
        _ => T("Cannot be opened"),
    };

    /// <summary>
    /// Explains why a preparation cannot be opened.
    /// </summary>
    /// <param name="status">The status of the preparation.</param>
    private string ProjectRecoveryMessage(BflProjectLoadStatus status) => status switch
    {
        BflProjectLoadStatus.NEWER_VERSION => T("A newer version of AI Studio stored this preparation. This version cannot open it. Please update AI Studio."),
        _ => T("AI Studio cannot read this preparation. Its files may be damaged."),
    };
}