using Runiq.AI.Core.Models;

namespace Runiq.AI.Core.AI.Capabilities;

/// <summary>
/// Thrown before provider invocation when a selected model does not declare a required operation.
/// </summary>
public sealed class UnsupportedModelCapabilityException : InvalidOperationException
{
    /// <summary>Initializes a capability validation failure without exposing provider credentials.</summary>
    /// <param name="model">The model that was selected.</param>
    /// <param name="requiredCapability">The capability required by the operation.</param>
    /// <param name="operation">The requested operation.</param>
    public UnsupportedModelCapabilityException(ModelReference model, ModelCapability requiredCapability, string operation)
        : base($"Model '{model.ProviderName}/{model.ModelName}' does not support required capability '{requiredCapability}' for operation '{operation}'.")
    {
        Model = model;
        RequiredCapability = requiredCapability;
        Operation = operation;
    }

    /// <summary>Gets the selected model.</summary>
    public ModelReference Model { get; }
    /// <summary>Gets the missing capability.</summary>
    public ModelCapability RequiredCapability { get; }
    /// <summary>Gets the requested operation.</summary>
    public string Operation { get; }
}
