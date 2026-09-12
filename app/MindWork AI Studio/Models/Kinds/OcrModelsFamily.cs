using AIStudio.Provider;

namespace AIStudio.Models.Kinds;

/// <summary>
/// The models which read text off a page.
/// </summary>
/// <remarks>
/// A document goes in and its text comes out. There is no conversation in them, so they answer a
/// chat completion request with an error rather than with a reply.
/// </remarks>
public sealed class OcrModelsFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.UNKNOWN;

    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.mistral.ai/capabilities/OCR/basic_ocr/", new DateOnly(2026, 9, 12), "Ported unchanged from the OCR marker of Provider/ModelKindExtensions.cs.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Modifier("ocr").AsSubstring().Kind(ModelKind.OCR);
}