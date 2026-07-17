using Microsoft.Extensions.Options;
using Runiq.AI.Rag.Configuration;

namespace Runiq.AI.Rag.Tests.Configuration;

public sealed class RagObservabilityOptionsTests
{
    // Verifies that the framework default never emits retrieved content previews.
    [Fact]
    public void Defaults_ShouldKeepContentPreviewDisabled()
    {
        var options = new RagObservabilityOptions();

        Assert.False(options.ContentPreview.Enabled);
        Assert.False(options.ContentPreview.IncludeSelectedResults);
        Assert.False(options.ContentPreview.IncludeRejectedResults);
    }

    // Verifies that a bounded selected-only preview configuration is accepted.
    [Fact]
    public void Validator_ShouldAcceptSelectedOnlyPreview()
    {
        var options = new RagObservabilityOptions();
        options.ContentPreview.Enabled = true;
        options.ContentPreview.IncludeSelectedResults = true;

        Assert.True(new RagObservabilityOptionsValidator().Validate(null, options).Succeeded);
    }

    // Verifies that preview lengths above the framework hard limit fail validation.
    [Fact]
    public void Validator_ShouldRejectPreviewAboveHardLimit()
    {
        var options = new RagObservabilityOptions();
        options.ContentPreview.Enabled = true;
        options.ContentPreview.IncludeSelectedResults = true;
        options.ContentPreview.MaximumCharacters = RagObservabilityOptions.MaximumContentPreviewCharacters + 1;

        Assert.False(new RagObservabilityOptionsValidator().Validate(null, options).Succeeded);
    }

    // Verifies that undefined query visibility values fail validation.
    [Fact]
    public void Validator_ShouldRejectUndefinedQueryVisibility()
    {
        var options = new RagObservabilityOptions { QueryVisibility = (RagQueryVisibility)999 };

        Assert.False(new RagObservabilityOptionsValidator().Validate(null, options).Succeeded);
    }

    // Verifies that metadata item and value bounds cannot exceed framework hard limits.
    [Fact]
    public void Validator_ShouldRejectMetadataBoundsAboveHardLimits()
    {
        var options = new RagObservabilityOptions
        {
            MetadataEntryLimit = RagObservabilityOptions.MaximumMetadataEntries + 1,
            MaximumMetadataValueLength = RagObservabilityOptions.MaximumMetadataValueCharacters + 1,
        };

        Assert.False(new RagObservabilityOptionsValidator().Validate(null, options).Succeeded);
    }
}
