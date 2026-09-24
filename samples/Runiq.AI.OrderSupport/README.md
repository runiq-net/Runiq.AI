# Runiq Order Support

This sample hosts two independent agents over shared deterministic in-memory order data:

- `Order Status Agent` uses only the `order_status` tool.
- `Return Eligibility Agent` uses only the `return_eligibility` tool.

## Run

Set `OpenAI:ApiKey` in `samples/Runiq.AI.OrderSupport/appsettings.Development.json`, then run from the repository root:

```powershell
dotnet run --project samples/Runiq.AI.OrderSupport/Runiq.AI.OrderSupport.csproj
```

Open the dashboard at `http://localhost:5130/dashboard` and select an agent.

## Example prompts

For `Order Status Agent`:

```text
What is the current status of order ORD-1001?
```

For `Return Eligibility Agent`:

```text
Can I return order ORD-1002, and why?
```

The committed `OpenAI:ApiKey` value is intentionally empty. You can also override it with normal ASP.NET Core configuration providers such as user secrets or the `OpenAI__ApiKey` environment variable.
