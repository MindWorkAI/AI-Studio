using System.Collections.Frozen;

using AIStudio.Settings.DataModel;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Reads the prompts and the knowledge base of the BeyondFacts Lab, which come with AI Studio.
/// </summary>
/// <remarks>
/// The files are German Markdown below Assistants/BeyondFactsLab, embedded into the app. A development build reads
/// them from the disk, so that a changed prompt shows in the next request without building again; see AppResources.
/// While the person allows it, files in the folder for prompt overrides replace them, see BflPromptOverrides.
/// </remarks>
public static class BflPromptLibrary
{
    /// <summary>
    /// The folder of the lab, relative to the project folder.
    /// </summary>
    private const string FOLDER = "Assistants/BeyondFactsLab";

    /// <summary>
    /// The prompt every chat of the lab starts with: role, principles, and the places of the other parts.
    /// </summary>
    public const string CORE_PATH = FOLDER + "/Prompts/core.md";

    /// <summary>
    /// The guide by Lara Wüster (TUM), which every chat of the lab keeps in its context.
    /// </summary>
    public const string KNOWLEDGE_PATH = FOLDER + "/Knowledge/beyondfacts-paper.md";

    /// <summary>
    /// The prompt of the conversation in which the lab tells whether it fits a task. It stands on its own, without
    /// the core and the guide.
    /// </summary>
    public const string ONBOARDING_PATH = FOLDER + "/Prompts/onboarding.md";

    /// <summary>
    /// The request which lets the model write the work state of a step, at the end of the conversation of the step.
    /// </summary>
    public const string WORK_STATE_PATH = FOLDER + "/Prompts/work-state.md";

    /// <summary>
    /// The folder of the requests which let the model write a result, one for each kind a model writes.
    /// </summary>
    private const string RESULTS_FOLDER = FOLDER + "/Prompts/results/";

    /// <summary>
    /// The instruction to address the person formally.
    /// </summary>
    private const string ADDRESS_FORMAL_PATH = FOLDER + "/Prompts/snippets/address-formal.md";

    /// <summary>
    /// The instruction to address the person informally.
    /// </summary>
    private const string ADDRESS_INFORMAL_PATH = FOLDER + "/Prompts/snippets/address-informal.md";

    /// <summary>
    /// The placeholders BflPromptComposer fills in the core.
    /// </summary>
    private static readonly FrozenSet<string> CORE_PLACEHOLDERS = FrozenSet.Create(StringComparer.Ordinal,
        BflPromptTemplate.RESPONSE_LANGUAGE,
        BflPromptTemplate.ADDRESS_FORM,
        BflPromptTemplate.KNOWLEDGE,
        BflPromptTemplate.STEP,
        BflPromptTemplate.MATERIALS,
        BflPromptTemplate.DETAILS,
        BflPromptTemplate.MISSING_DETAILS,
        BflPromptTemplate.WORK_STATES);

    /// <summary>
    /// The placeholders BflPromptComposer fills in the prompt of the onboarding.
    /// </summary>
    private static readonly FrozenSet<string> ONBOARDING_PLACEHOLDERS = FrozenSet.Create(StringComparer.Ordinal,
        BflPromptTemplate.RESPONSE_LANGUAGE,
        BflPromptTemplate.ADDRESS_FORM);

    /// <summary>
    /// The placeholders BflPromptComposer fills in the request for a work state.
    /// </summary>
    private static readonly FrozenSet<string> WORK_STATE_PLACEHOLDERS = FrozenSet.Create(StringComparer.Ordinal,
        BflPromptTemplate.SECTIONS,
        BflPromptTemplate.FIELDS);

    /// <summary>
    /// The placeholders BflPromptComposer fills in the request for a result.
    /// </summary>
    private static readonly FrozenSet<string> RESULT_PLACEHOLDERS = FrozenSet.Create(StringComparer.Ordinal,
        BflPromptTemplate.MISSING_WORK_STATES);

    /// <summary>
    /// The placeholders of every other file: none, because BflPromptComposer inserts these files as values.
    /// </summary>
    private static readonly FrozenSet<string> NO_PLACEHOLDERS = FrozenSet<string>.Empty;

    /// <summary>
    /// Gets the prompt of a step, named like the folder of the step.
    /// </summary>
    /// <param name="step">The step.</param>
    public static string StepPath(BflStep step) => $"{FOLDER}/Prompts/steps/{step.FolderName}.md";

    /// <summary>
    /// Gets the request which lets the model write a result.
    /// </summary>
    /// <param name="kind">The kind of result, one a model writes.</param>
    /// <exception cref="ArgumentOutOfRangeException">No model writes this kind of result.</exception>
    public static string ResultPath(BflResultKind kind) => kind switch
    {
        BflResultKind.POSITION_PAPER => RESULTS_FOLDER + "position-paper.md",
        BflResultKind.CONVERSATION_GUIDE => RESULTS_FOLDER + "conversation-guide.md",
        BflResultKind.QUESTIONS_AND_ANSWERS => RESULTS_FOLDER + "questions-and-answers.md",
        BflResultKind.OPEN_POINTS => RESULTS_FOLDER + "open-points.md",

        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No model writes this result.")
    };

    /// <summary>
    /// Gets the instruction for a form of address.
    /// </summary>
    /// <param name="addressForm">The form of address.</param>
    public static string AddressPath(AddressForm addressForm) => addressForm is AddressForm.INFORMAL ? ADDRESS_INFORMAL_PATH : ADDRESS_FORMAL_PATH;

    /// <summary>
    /// Gets every file of the lab's prompts, e.g., to check that the app comes with all of them.
    /// </summary>
    public static IReadOnlyList<string> AllPaths =>
    [
        CORE_PATH,
        KNOWLEDGE_PATH,
        ONBOARDING_PATH,
        WORK_STATE_PATH,
        ..BflResultKind.All.Where(kind => kind.IsWrittenByModel).Select(ResultPath),
        ..Enum.GetValues<AddressForm>().Select(AddressPath).Distinct(),
        ..BflStep.All.Select(StepPath)
    ];

    /// <summary>
    /// Gets a file of the lab's prompts relative to the folder of the lab, e.g., "Prompts/core.md".
    /// </summary>
    /// <param name="path">The file, one of the paths of this class.</param>
    /// <exception cref="ArgumentException">The file lies outside the folder of the lab.</exception>
    public static string RelativePath(string path) => path.StartsWith(FOLDER + "/", StringComparison.Ordinal) ? path[(FOLDER.Length + 1)..] : throw new ArgumentException("The file lies outside the folder of the BeyondFacts Lab.", nameof(path));

    /// <summary>
    /// Gets the placeholders a file of the lab's prompts may contain.
    /// </summary>
    /// <remarks>
    /// BflPromptComposer fills placeholders in the core, in the prompt of the onboarding, and in the requests for a work
    /// state and for a result only. It inserts every other file as a value, whose placeholders it leaves as they are, so
    /// that they would reach the model as text.
    /// </remarks>
    /// <param name="path">The file, one of the paths of this class.</param>
    public static IReadOnlySet<string> PlaceholdersOf(string path) => path switch
    {
        CORE_PATH => CORE_PLACEHOLDERS,
        ONBOARDING_PATH => ONBOARDING_PLACEHOLDERS,
        WORK_STATE_PATH => WORK_STATE_PLACEHOLDERS,
        _ when path.StartsWith(RESULTS_FOLDER, StringComparison.Ordinal) => RESULT_PLACEHOLDERS,
        _ => NO_PLACEHOLDERS
    };

    /// <summary>
    /// Reads the texts of the system prompt of a step.
    /// </summary>
    /// <param name="step">The step.</param>
    /// <param name="addressForm">How the model addresses the person.</param>
    /// <param name="overrideDirectory">The folder whose files replace the prompts, or null while the person does not allow it.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The texts, which BflPromptComposer puts together.</returns>
    /// <exception cref="FileNotFoundException">The app comes without one of the files.</exception>
    /// <exception cref="BflPromptOverrideException">A file which replaces a prompt contains a placeholder which the lab does not fill in it.</exception>
    public static async Task<BflPromptParts> LoadPartsAsync(BflStep step, AddressForm addressForm, string? overrideDirectory = null, CancellationToken token = default) => new(
        await ReadAsync(CORE_PATH, overrideDirectory, token),
        await ReadAsync(KNOWLEDGE_PATH, overrideDirectory, token),
        await ReadAsync(StepPath(step), overrideDirectory, token),
        await ReadAsync(AddressPath(addressForm), overrideDirectory, token));

    /// <summary>
    /// Reads the texts of the system prompt of the conversation about whether the lab fits a task.
    /// </summary>
    /// <param name="addressForm">How the model addresses the person.</param>
    /// <param name="overrideDirectory">The folder whose files replace the prompts, or null while the person does not allow it.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The texts, which BflPromptComposer puts together.</returns>
    /// <exception cref="FileNotFoundException">The app comes without one of the files.</exception>
    /// <exception cref="BflPromptOverrideException">A file which replaces a prompt contains a placeholder which the lab does not fill in it.</exception>
    public static async Task<BflOnboardingPromptParts> LoadOnboardingPartsAsync(AddressForm addressForm, string? overrideDirectory = null, CancellationToken token = default) => new(
        await ReadAsync(ONBOARDING_PATH, overrideDirectory, token),
        await ReadAsync(AddressPath(addressForm), overrideDirectory, token));

    /// <summary>
    /// Reads a file of the lab's prompts: the file which replaces it, if there is one, or else the file which comes
    /// with the app.
    /// </summary>
    /// <param name="path">The file, one of the paths of this class.</param>
    /// <param name="overrideDirectory">The folder whose files replace the prompts, or null while the person does not allow it.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The text of the file.</returns>
    /// <exception cref="FileNotFoundException">The app comes without the file, or the file is empty.</exception>
    /// <exception cref="BflPromptOverrideException">The file which replaces the prompt contains a placeholder which the lab does not fill in it.</exception>
    public static async Task<string> ReadAsync(string path, string? overrideDirectory = null, CancellationToken token = default)
    {
        if (overrideDirectory is not null && await BflPromptOverrides.TryReadAsync(overrideDirectory, path, token) is { } replacement)
            return replacement;

        return await ReadBuiltInAsync(path, token);
    }

    /// <summary>
    /// Reads a file of the lab's prompts as it comes with the app.
    /// </summary>
    /// <param name="path">The file, one of the paths of this class.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The text of the file.</returns>
    /// <exception cref="FileNotFoundException">The app comes without the file, or the file is empty.</exception>
    public static async Task<string> ReadBuiltInAsync(string path, CancellationToken token = default)
    {
        var text = await AppResources.ReadTextAsync(path, token);
        if (string.IsNullOrWhiteSpace(text))
            throw new FileNotFoundException("The BeyondFacts Lab misses one of its prompts.", path);

        return text;
    }
}