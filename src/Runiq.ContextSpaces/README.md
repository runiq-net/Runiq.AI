# Runiq.ContextSpaces

![NuGet Version](https://img.shields.io/nuget/vpre/Runiq.ContextSpaces?label=nuget)

Context spaces and source-reading primitives for Runiq.Net agents.

`Runiq.ContextSpaces` provides the building blocks for attaching read-only domain context to agents. Use it to define reusable context spaces, register source providers, discover skill documents, and expose supported source content through the Runiq.Net runtime and dashboard.

## Why Runiq.ContextSpaces?

AI agents usually need more than instructions and tools. They often need access to domain-specific documents, rules, guides, policies, examples, or reusable knowledge sources.

`Runiq.ContextSpaces` keeps that context separate from agent definitions.

It focuses on:

- Reusable context space definitions
- File-system backed source registration
- Read-only context access for agents
- Skill document discovery
- Dashboard-visible context browsing
- Clean separation between agent behavior and domain knowledge

## Install

```powershell
dotnet add package Runiq.ContextSpaces --prerelease
```

## Create a Context Space

```csharp
using Runiq.ContextSpaces.Models.Sources;

var contextSpace = new ContextSpace(
        id: "travel-planning",
        name: "Travel Planning",
        description: "Shared travel planning context.")
    .AddSources(sources => sources.FromFileSystem(
        id: "travel-documents",
        name: "Travel Documents",
        path: "content/travel"));
```

A context space defines a named context boundary.

Typical fields:

- `id`: stable identifier used by agents and runtime endpoints
- `name`: human-readable context space name
- `description`: short explanation of the context purpose
- `sources`: registered locations where context content can be discovered

## File-System Sources

A file-system source allows agents and dashboard endpoints to discover supported local content.

```csharp
var contextSpace = new ContextSpace(
        id: "company-policy",
        name: "Company Policy",
        description: "Internal company policy documents.")
    .AddSources(sources => sources.FromFileSystem(
        id: "policy-documents",
        name: "Policy Documents",
        path: "content/policies"));
```

This keeps your domain material outside the agent definition while still making it available to the Runiq.Net runtime.

## Skill Documents

Context spaces can also be used to organize skill-oriented documentation.

A typical structure may look like this:

```text
content/
  travel/
    istanbul-guide.md
    bursa-guide.md
    london-city-guide.pdf
  skills/
    trip-planning/
      SKILL.md
```

This allows reusable instructions, examples, and domain documents to live alongside your application without hardcoding them into the agent.

## Typical Use Cases

Use `Runiq.ContextSpaces` when you want to:

- Provide domain documents to agents
- Keep context separate from agent instructions
- Group reusable knowledge by business area
- Register file-system backed source folders
- Discover and preview skill documentation
- Build dashboard-visible context and source browsing experiences

## Example Scenarios

| Scenario | Context Space Example |
|---|---|
| Travel assistant | City guides, planning rules, travel documents |
| Support assistant | Product manuals, FAQ files, troubleshooting guides |
| HR assistant | Policy documents, onboarding guides, internal procedures |
| Finance assistant | Expense rules, reporting templates, accounting notes |
| Legal assistant | Contract templates, policy references, compliance notes |

## Related Packages

Runiq.Net is modular. `Runiq.ContextSpaces` can be used together with other Runiq packages:

| Package | Purpose |
|---|---|
| `Runiq.Agents` | Defines agents and tool execution primitives |
| `Runiq.Core` | Hosts agents and the embedded dashboard in ASP.NET Core |
| `Runiq.Workflows` | Orchestrates agents in code-first workflows |
| `Runiq.Mcp` | Exposes ASP.NET Core applications through MCP-compatible tools |

## Documentation

Full documentation is available at:

https://runiq.net/docs

## Status

Runiq.Net is currently in preview.

APIs may change before the first stable release.

The main direction is clear:

> Build code-first AI agents, tools, workflows, context sources, MCP endpoints, and dashboards for .NET applications.

## License

MIT