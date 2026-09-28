# Runiq Incident Triage Assistant

This sample demonstrates two local CLI-backed agents that triage the same backend incident with the same strongly typed C# tools:

- **Incident Triage Assistant - Codex**
- **Incident Triage Assistant - Claude**

The scenario is a deterministic demo incident for an e-commerce `order-api` service. Error rate and latency increase after a nearby deployment. The agents use local JSON fixture data only. There is no real monitoring, cloud, production, rollback, or remediation integration.

## Architecture

The sample uses the existing Runiq agent hosting APIs:

- `AddRuniqServer(...)` registers the local Dashboard, agents, executors, and tool bridge.
- `UseCodex(...)` configures the Codex CLI-backed agent.
- `UseClaude(...)` configures the Claude CLI-backed agent.
- `AddTool<TTool>()` attaches strongly typed tools to each agent.
- `IIncidentDataSource` is registered in dependency injection and is injected into all tools.

Both agents share the same instructions and tools:

- `get_service_metrics`
- `get_recent_deployments`
- `get_runbook`

## Requirements

- .NET 10 SDK
- Codex CLI installed and authenticated for the Codex agent
- Claude CLI installed and authenticated for the Claude agent

Unit tests do not require either CLI.

You can verify that the local CLI commands are available with:

```powershell
codex --version
claude --version
```

## Run the sample

From the repository root:

```powershell
dotnet run --project samples/Runiq.AI.CLI.Agents --launch-profile http
```

Open the Dashboard:

```text
http://localhost:5308/dashboard
```

The Dashboard is configured with anonymous access for local demo use only. Do not expose this sample host directly to the internet.

## Example prompt

Use the same prompt with either agent:

```text
Triage the Order API incident for service order-api between 2026-09-28T09:30:00Z and 2026-09-28T10:30:00Z. Use the available incident tools and return anomalies, deployment correlation, likely causes, first checks, and sources. Do not perform production changes or recommend automatic rollback.
```

## Expected behavior

The selected agent should call all three tools:

1. `get_service_metrics` to inspect error rate, latency, throughput, and dependency timeout anomalies.
2. `get_recent_deployments` to inspect nearby demo deployment records.
3. `get_runbook` to inspect first-response guidance for the incident type.

The final answer should include these headings:

- Tespit edilen anormallikler
- Deployment ile zamansal ilişki
- Olası nedenler
- İlk kontrol adımları
- Kullanılan kaynaklar

The answer should describe deployment timing as correlation, not proof of root cause. It should not claim to modify a real system.

## Manual smoke testing

The expected behavior above is a checklist, not a verified transcript. This README intentionally does not claim that Claude or Codex succeeded in your environment. Run each agent manually from the Dashboard after confirming that the matching CLI is installed and authenticated.

Record a verified transcript only after the specific CLI run has completed successfully in your environment.

## Run tests

```powershell
dotnet test tests/Runiq.AI.CLI.Agents.Tests
```

The tests validate JSON fixture parsing, unknown service behavior, metric and deployment filtering, tool input/output behavior, agent executor selection, shared tool registration, and fixture accessibility without starting Codex or Claude.
