# Runiq.ContextSpaces

Context and source reading primitives for Runiq.Net agents.

`Runiq.ContextSpaces` provides the building blocks for attaching read-only context to agents. Use it to define context spaces, register source providers, discover skill documents, and preview supported source content through the Runiq runtime and dashboard.

## Install

```powershell
dotnet add package Runiq.ContextSpaces --version 0.1.0-preview.1
```

## Basic Context Space

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

Context spaces can be attached to agents by id and surfaced through `Runiq.Core` dashboard endpoints.

## Typical Use Cases

- Provide file-system backed context documents to agents.
- Discover and preview skill documentation.
- Keep reusable domain context separate from agent definitions.
- Build dashboard-visible source and skill browsing experiences.

## Related Packages

- `Runiq.Agents` defines agents and tool execution primitives.
- `Runiq.Core` hosts the dashboard and runtime endpoints.

## Documentation

Full documentation is available at [runiq.net/docs](https://runiq.net/docs).
