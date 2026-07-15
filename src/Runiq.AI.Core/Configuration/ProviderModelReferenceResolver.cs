using Runiq.AI.Core.AI.Capabilities;
using Runiq.AI.Core.Models;

namespace Runiq.AI.Core.Configuration;

/// <summary>
/// Projects a named provider model configuration into the Core <see cref="ModelReference"/> consumed by clients.
/// </summary>
public static class ProviderModelReferenceResolver
{
    /// <summary>
    /// Resolves a configured model registration. Named registrations take precedence over capabilities already
    /// attached to <paramref name="model"/>; unconfigured models are returned unchanged.
    /// </summary>
    /// <param name="model">The selected provider and model identity.</param>
    /// <param name="provider">The existing provider options containing named model registrations.</param>
    /// <returns>The model identity carrying an explicit configured capability set when a registration matches.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when configured embedding dimensions are invalid.</exception>
    public static ModelReference Resolve(ModelReference model, ProviderOptions? provider)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (provider?.Models is null || provider.Models.Count == 0)
            return model;

        var registration = provider.Models.FirstOrDefault(pair =>
            string.Equals(pair.Key, model.ModelName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(pair.Value.Model, model.ModelName, StringComparison.OrdinalIgnoreCase)).Value;

        if (registration?.Capabilities is null)
            return model;

        if (registration.EmbeddingDimensions is < 1)
            throw new ArgumentOutOfRangeException(nameof(provider), "Configured embedding dimensions must be greater than zero.");

        var capabilities = registration.Capabilities.Aggregate(ModelCapability.None, static (current, capability) => current | capability);
        var configuredModel = string.IsNullOrWhiteSpace(registration.Model) ? model : model.WithModelName(registration.Model);
        return configuredModel.WithCapabilities(capabilities, registration.EmbeddingDimensions);
    }
}
