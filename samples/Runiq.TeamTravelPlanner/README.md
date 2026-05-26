# Runiq Team Travel Planner

This sample demonstrates Agent Teams in Runiq.Net with two travel-planning teams:

- `Travel Planning Team`: fixed sequential execution in declared member order.
- `Adaptive Travel Planning Team`: LLM-guided member selection for each user request.

Both teams use the same four agents:

- Weather Agent with `WeatherTool`
- Budget Agent with `BudgetEstimatorTool`
- Places Agent with `PlacesTool`
- Planner Agent with `MealSuggestionTool`

The adaptive team first asks the model to create an execution plan from the user request and declared team members. It is not keyword routing; the model chooses the useful members and order, and the runtime validates that only declared agents can run.

The tools use deterministic in-memory demo data and do not require external API keys or paid services. Model calls require an OpenAI API key.

## Run

From the repository root:

```powershell
dotnet run --project samples/Runiq.TeamTravelPlanner/Runiq.TeamTravelPlanner.csproj
```

Dashboard:

```text
http://localhost:5127/dashboard
```

Agent Teams:

```text
http://localhost:5127/dashboard/teams
```

The launch profile uses the Development environment and opens `/dashboard`.

## Example Prompts

Use `Travel Planning Team` to see the fixed sequential trace:

```text
İstanbul için 2 kişilik, 1 günlük pratik tarihi gezi planı hazırla. Çok yorucu olmasın.
```

Expected sequential trace: Weather Analyst, Budget Analyst, Places Researcher, then Travel Planner.

Use `Adaptive Travel Planning Team` to see model-guided member selection:

```text
İstanbul için 2 kişilik, 1 günlük pratik tarihi gezi planı hazırla. Çok yorucu olmasın.
```

Expected: adaptive mode may include weather, places, budget, and planner because outdoor comfort, historical places, people count, and final synthesis all matter.

```text
İstanbul’da yürüyüş için hava uygun mu?
```

Expected: adaptive mode may only need weather-related work and should not blindly run budget or places agents.

```text
İzmir için 4 kişilik ekonomik 2 günlük gezi planı hazırla.
```

Expected: adaptive mode may include budget, places, planner, and possibly weather depending on the model's plan.

## Configuration

The agents and adaptive planner use the repository's current sample model naming style, `openai/gpt-5`. To execute model calls, configure `OpenAI:ApiKey` through user secrets, `appsettings.Development.json`, or environment variables:

```json
{
  "OpenAI": {
    "ApiKey": "..."
  }
}
```
