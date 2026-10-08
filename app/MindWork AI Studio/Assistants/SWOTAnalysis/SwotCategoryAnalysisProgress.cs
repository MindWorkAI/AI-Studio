namespace AIStudio.Assistants.SWOTAnalysis;

internal enum SwotCategoryAnalysisStatus
{
    RUNNING,
    COMPLETED,
    FAILED,
    CANCELED,
}

internal sealed record SwotCategoryAnalysisProgress(
    SwotCategoryAnalysisStatus Status,
    SwotCategoryAnalysisResult? Result = null,
    string Message = "");