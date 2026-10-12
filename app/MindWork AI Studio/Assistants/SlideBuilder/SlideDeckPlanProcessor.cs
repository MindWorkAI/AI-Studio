using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Assistants.SlideBuilder;

/// <summary>
/// Parses, validates, repairs, and previews the structured slide plan.
/// </summary>
internal static class SlideDeckPlanProcessor
{
    internal const int CONTRACT_VERSION = 1;
    internal const int MAX_TOTAL_SLIDES = 20;
    internal const int MAX_BULLET_POINTS = 7;
    private const int MAX_WORDS_PER_BULLET = 12;

    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>
    /// Gets the strict JSON grammar included in the model prompt.
    /// </summary>
    internal static string ContractGrammar => StructuredResponseProcessor.BuildContractGrammar<SlideDeckPlan>();

    /// <summary>
    /// Parses and semantically validates a model response.
    /// </summary>
    /// <param name="answer">The complete model response.</param>
    /// <param name="requiredTotalSlides">The exact total including the title slide, or zero for model choice.</param>
    /// <param name="requiredBulletPoints">The exact bullets per content slide, or zero for model choice.</param>
    /// <returns>The accepted plan or a safe rejection.</returns>
    internal static SlideDeckPlanProcessingResult Process(
        string answer,
        int requiredTotalSlides,
        int requiredBulletPoints)
    {
        var result = StructuredResponseProcessor.Process<SlideDeckPlan, string>(
            answer,
            JSON_OPTIONS,
            (_, _) => "STRUCTURE_INVALID",
            plan => Validate(plan, requiredTotalSlides, requiredBulletPoints));

        return result.Response is not null
            ? new(result.Response, string.Empty, null)
            : new(null, result.Issue!.Issue, result.Issue.Diagnostic);
    }

    /// <summary>
    /// Builds the content-free instruction for the single same-context repair attempt.
    /// </summary>
    internal static string BuildRepairPrompt(SlideDeckPlanProcessingResult result)
    {
        var diagnostic = result.Diagnostic;
        var location = diagnostic is null
            ? string.Empty
            : $"""
               Structural issue: {diagnostic.IssueKind}
               JSON path: {diagnostic.JsonPath}
               Unknown or missing field: {(string.IsNullOrEmpty(diagnostic.FieldName) ? "none" : diagnostic.FieldName)}
               Expected shape: {(string.IsNullOrEmpty(diagnostic.Expected) ? "the active contract" : diagnostic.Expected)}
               """;

        return $"""
                Correct the complete preceding assistant response so it satisfies the same strict slide-deck contract.
                Return the entire corrected JSON object without explanation, Markdown, or a code fence.
                Do not return a patch and do not repeat the source material.
                Validation issue: {result.Issue}
                {location}
                """;
    }

    /// <summary>
    /// Converts a validated plan into the existing readable Markdown result.
    /// </summary>
    internal static string ToMarkdown(SlideDeckPlan plan)
    {
        var result = new StringBuilder();
        result.Append("# ");
        result.AppendLine(plan.Title.Trim());

        foreach (var slide in plan.Slides)
        {
            result.AppendLine();
            result.Append("## ");
            result.AppendLine(slide.Title.Trim());
            foreach (var bulletPoint in slide.BulletPoints)
            {
                result.Append("- ");
                result.AppendLine(bulletPoint.Trim());
            }
        }

        return result.ToString().TrimEnd().ReplaceLineEndings("\n");
    }

    private static StructuredResponseIssue<string>? Validate(
        SlideDeckPlan plan,
        int requiredTotalSlides,
        int requiredBulletPoints)
    {
        if (plan.ContractVersion != CONTRACT_VERSION)
            return Issue("The slide plan uses an unsupported contract version.", "$.contractVersion", CONTRACT_VERSION.ToString());

        if (string.IsNullOrWhiteSpace(plan.Title))
            return Issue("The presentation title must not be empty.", "$.title", "non-empty string");

        var totalSlides = plan.Slides.Count + 1;
        if (requiredTotalSlides > 0 && totalSlides != requiredTotalSlides)
            return Issue("The slide plan does not contain the requested total number of slides.", "$.slides", $"array producing {requiredTotalSlides} total slides including the title slide");

        if (requiredTotalSlides == 0 && totalSlides is < 2 or > MAX_TOTAL_SLIDES)
            return Issue("The slide plan must contain between 2 and 20 slides including the title slide.", "$.slides", "array producing 2 to 20 total slides including the title slide");

        for (var slideIndex = 0; slideIndex < plan.Slides.Count; slideIndex++)
        {
            var slide = plan.Slides[slideIndex];
            if (string.IsNullOrWhiteSpace(slide.Title))
                return Issue("Every content slide must have a title.", $"$.slides[{slideIndex}].title", "non-empty string");

            if (requiredBulletPoints > 0 && slide.BulletPoints.Count != requiredBulletPoints)
                return Issue("A content slide does not contain the requested number of bullet points.", $"$.slides[{slideIndex}].bulletPoints", $"array with {requiredBulletPoints} items");

            if (requiredBulletPoints == 0 && slide.BulletPoints.Count is < 1 or > MAX_BULLET_POINTS)
                return Issue("Every content slide must contain between 1 and 7 bullet points.", $"$.slides[{slideIndex}].bulletPoints", "array with 1 to 7 items");

            for (var bulletIndex = 0; bulletIndex < slide.BulletPoints.Count; bulletIndex++)
            {
                var bulletPoint = slide.BulletPoints[bulletIndex];
                var path = $"$.slides[{slideIndex}].bulletPoints[{bulletIndex}]";
                if (string.IsNullOrWhiteSpace(bulletPoint))
                    return Issue("Bullet points must not be empty.", path, "non-empty string");

                if (CountWords(bulletPoint) > MAX_WORDS_PER_BULLET)
                    return Issue("A bullet point contains more than 12 words.", path, "string with at most 12 words");
            }
        }

        return null;
    }

    private static StructuredResponseIssue<string> Issue(string issue, string path, string expected) => new(
        issue,
        "SEMANTIC_INVALID",
        new()
        {
            IssueKind = StructuredResponseIssueKind.SEMANTIC_CONTRACT_INVALID,
            JsonPath = path,
            Expected = expected,
        });

    private static int CountWords(string value) => value
        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
        .Length;
}
