using System.Text.Json;

namespace AIStudio.Assistants.BeyondFactsLab;

public sealed partial class BeyondFactsLabStore
{
    /// <summary>
    /// The folder of a preparation which holds a folder for each step.
    /// </summary>
    private const string STEPS_DIRECTORY_NAME = "steps";

    /// <summary>
    /// The file of a step which holds its form "Your details".
    /// </summary>
    private const string FORM_FILE_NAME = "form.json";

    /// <summary>
    /// Loads the form of a step.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="step">The step.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The form, or an empty one when the person entered nothing yet.</returns>
    /// <exception cref="InvalidDataException">This version cannot open the form.</exception>
    public async Task<BflStepFormData> LoadFormAsync(Guid projectId, BflStep step, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        return await this.LoadFormWithoutInitializeAsync(projectId, step, token);
    }

    /// <summary>
    /// Loads the forms of every step of a preparation, e.g., to find missing details.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The forms this version can open, by their steps.</returns>
    public async Task<IReadOnlyDictionary<BflStep, BflStepFormData>> LoadFormsAsync(Guid projectId, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var forms = new Dictionary<BflStep, BflStepFormData>();
        foreach (var step in BflStep.All)
        {
            try
            {
                forms[step] = await this.LoadFormWithoutInitializeAsync(projectId, step, token);
            }
            catch (InvalidDataException exception)
            {
                logger.LogWarning(exception, "Could not open the form of the step '{Step}' of the preparation '{ProjectId}' of the BeyondFacts Lab.", step, projectId);
            }
        }

        return forms;
    }

    /// <summary>
    /// Stores the form of a step and counts it as a change of the preparation.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="step">The step.</param>
    /// <param name="form">The form.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The preparation with its new time of change.</returns>
    /// <exception cref="FileNotFoundException">The preparation does not exist.</exception>
    /// <exception cref="InvalidDataException">This version cannot open the preparation or the stored form, so it does not write them either.</exception>
    public async Task<BflProject> SaveFormAsync(Guid projectId, BflStep step, BflStepFormData form, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var gate = this.GetLock(projectId);
        await gate.WaitAsync(token);
        try
        {
            var project = await this.LoadRequiredWithoutInitializeAsync(projectId, token);

            // A form which a newer version stored, or which is damaged, stays as it is:
            await this.LoadFormWithoutInitializeAsync(projectId, step, token);

            form.SchemaVersion = BflStepFormData.CURRENT_SCHEMA_VERSION;
            await WriteTextAtomicAsync(this.FormPath(projectId, step), JsonSerializer.Serialize(form, JSON_OPTIONS), token);

            project.ModifiedAtUtc = DateTimeOffset.UtcNow;
            await this.StoreProjectAsync(project, token);
            return project;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Reads the form of a step without the first start of the store.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="step">The step.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The form, or an empty one when there is no file.</returns>
    /// <exception cref="InvalidDataException">This version cannot open the form.</exception>
    private async Task<BflStepFormData> LoadFormWithoutInitializeAsync(Guid projectId, BflStep step, CancellationToken token)
    {
        var path = this.FormPath(projectId, step);
        if (!File.Exists(path))
            return new BflStepFormData();

        BflStepFormData? form;
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, true);
            form = await JsonSerializer.DeserializeAsync<BflStepFormData>(stream, JSON_OPTIONS, token);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new InvalidDataException("This version of AI Studio cannot open the form.", exception);
        }

        if (form is null || form.SchemaVersion is < 1 or > BflStepFormData.CURRENT_SCHEMA_VERSION || IsNull(form.Values))
            throw new InvalidDataException("This version of AI Studio cannot open the form.");

        return form;
    }

    /// <summary>
    /// Gets the file of the form of a step.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    /// <param name="step">The step.</param>
    private string FormPath(Guid projectId, BflStep step) => Path.Combine(this.ProjectDirectory(projectId), STEPS_DIRECTORY_NAME, step.FolderName, FORM_FILE_NAME);

    /// <summary>
    /// Checks for null a value which the type system says cannot be null, but which JSON can make null.
    /// </summary>
    /// <param name="value">The value.</param>
    private static bool IsNull<T>(T? value) => value is null;
}