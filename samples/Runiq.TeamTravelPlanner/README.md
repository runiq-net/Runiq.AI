# Runiq Team Travel Planner

This sample demonstrates Agent Teams in Runiq.Net with a sequential travel-planning team.

The app hosts the Runiq Dashboard and registers four agents that collaborate on a practical city travel request:

- Weather Agent with `WeatherTool`
- Budget Agent with `BudgetEstimatorTool`
- Places Agent with `PlacesTool`
- Planner Agent with `MealSuggestionTool`

The tools use deterministic in-memory demo data and do not require external API keys or paid services.

## Run

From the repository root:

```powershell
dotnet run --project samples/Runiq.TeamTravelPlanner/Runiq.TeamTravelPlanner.csproj
```

Dashboard:

```text
http://localhost:5127/dashboard
```

The launch profile uses the Development environment and opens `/dashboard`.

## Example Prompt

```text
İstanbul için 2 kişilik, 1 günlük pratik tarihi gezi planı hazırla. Çok yorucu olmasın.
```

Open the Agent Teams page, select Travel Planning Team, and run the prompt in the team playground. The sequential trace should show Weather Analyst, Budget Analyst, Places Researcher, and Travel Planner member cards.

## Notes

The agents use the repository's current sample model naming style, `openai/gpt-5`. To execute model calls, configure `OpenAI:ApiKey` through user secrets, `appsettings.Development.json`, or environment variables. Tool execution itself is deterministic and local.
