namespace AIStudio.Provider;

public sealed record ModelLoadResult(
    IReadOnlyList<Model> Models,
    ModelLoadFailureReason FailureReason = ModelLoadFailureReason.NONE,
    string? TechnicalDetails = null)
{
    public bool Success => this.FailureReason is ModelLoadFailureReason.NONE;

    public static ModelLoadResult FromModels(IEnumerable<Model> models)
    {
        return new([..models]);
    }

    public static ModelLoadResult Failure(ModelLoadFailureReason failureReason, string? technicalDetails = null)
    {
        return new([], failureReason, technicalDetails);
    }
}