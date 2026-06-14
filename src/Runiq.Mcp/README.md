# Runiq.Mcp

`Runiq.Mcp` lets an ASP.NET Core application expose its application services as MCP tools.

## Installation

```csharp
builder.Services.AddRuniqMcp();

var app = builder.Build();

app.MapRuniqMcp();

app.Run();
```

By default, the MCP endpoint is available at:

```bash
/mcp
```

## Creating an MCP tool

MCP tools can use regular ASP.NET Core dependency injection services.

```csharp
using ModelContextProtocol.Server;
using System.ComponentModel;

[McpServerToolType]
public sealed class OrderStatusMcpTool
{
    private readonly IOrderService _orders;

    public OrderStatusMcpTool(IOrderService orders)
    {
        _orders = orders;
    }

    [McpServerTool]
    [Description("Gets the current status of an order.")]
    public Task<OrderStatusResult> GetOrderStatus(
        [Description("The order identifier.")] string orderId)
    {
        return _orders.GetStatusAsync(orderId);
    }
}
```

Register the service as usual:

```csharp
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddRuniqMcp();
```

Then map the MCP endpoint:

```csharp
app.MapRuniqMcp();
```

## What this enables

Existing ASP.NET Core services can be exposed to MCP-compatible clients without rewriting the application around a new runtime.

ASP.NET Core service → Runiq.Mcp → MCP tool

## Current scope

The current preview focuses on MCP tools.

Planned areas:

- MCP resources
- MCP prompts
- Runiq native tool exposure
- MCP client integration

