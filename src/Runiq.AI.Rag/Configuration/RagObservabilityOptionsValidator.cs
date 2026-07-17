using Microsoft.Extensions.Options;

namespace Runiq.AI.Rag.Configuration;

internal sealed class RagObservabilityOptionsValidator : IValidateOptions<RagObservabilityOptions>
{
    public ValidateOptionsResult Validate(string? name, RagObservabilityOptions options)
    {
        if (!Enum.IsDefined(options.QueryVisibility)) return ValidateOptionsResult.Fail("QueryVisibility is not defined.");
        if (options.MaximumQueryCharacters <= 0) return ValidateOptionsResult.Fail("MaximumQueryCharacters must be greater than zero.");
        if (string.IsNullOrWhiteSpace(options.RedactedQueryPlaceholder)) return ValidateOptionsResult.Fail("RedactedQueryPlaceholder is required.");
        if (options.MetadataEntryLimit is < 0 or > RagObservabilityOptions.MaximumMetadataEntries) return ValidateOptionsResult.Fail($"MetadataEntryLimit must be between zero and {RagObservabilityOptions.MaximumMetadataEntries}.");
        if (options.MaximumMetadataValueLength is <= 0 or > RagObservabilityOptions.MaximumMetadataValueCharacters) return ValidateOptionsResult.Fail($"MaximumMetadataValueLength must be between one and {RagObservabilityOptions.MaximumMetadataValueCharacters}.");
        if (options.SafeMetadataKeys.Any(string.IsNullOrWhiteSpace)) return ValidateOptionsResult.Fail("SafeMetadataKeys cannot contain empty keys.");
        var preview = options.ContentPreview;
        if (preview.MaximumCharacters <= 0) return ValidateOptionsResult.Fail("ContentPreview.MaximumCharacters must be greater than zero.");
        if (preview.MaximumCharacters > RagObservabilityOptions.MaximumContentPreviewCharacters) return ValidateOptionsResult.Fail($"ContentPreview.MaximumCharacters cannot exceed {RagObservabilityOptions.MaximumContentPreviewCharacters}.");
        if (!preview.Enabled && (preview.IncludeSelectedResults || preview.IncludeRejectedResults)) return ValidateOptionsResult.Fail("Result preview flags require ContentPreview.Enabled.");
        if (preview.Enabled && !preview.IncludeSelectedResults && !preview.IncludeRejectedResults) return ValidateOptionsResult.Fail("Enabled content previews must include selected or rejected results.");
        return ValidateOptionsResult.Success;
    }
}
