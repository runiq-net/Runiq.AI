using Runiq.Agents;

namespace Runiq.ContextTravelGuide.Agents;

public static class PlainAgent
{
    public static Agent Create(string? apiKey)
    {
        return new Agent(
            id: "plain-agent",
            name: "Plain Agent",
            instructions: "You are a simple assistant. Answer clearly and briefly.",
            model: "openai/gpt-5",
            apiKey: apiKey);
    }
}
