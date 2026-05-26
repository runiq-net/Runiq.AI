# Runiq Expense Desk

Runiq Expense Desk demonstrates how a .NET application can give an AI agent controlled access to structured business data through typed tools.

This sample is focused on employee expense analysis. The Expense Data Analyst answers business questions by calling a typed .NET tool that queries structured SQL data. It is not a tourism/context demo, and it is not a production financial system.

## What This Sample Demonstrates

- Registering an agent from a dedicated `Agents` folder
- Exposing a typed .NET tool to the agent
- Querying structured expense rows from an in-memory SQLite database
- Filtering by year, month, employee, department, and category
- Producing totals, rankings, and grouped summaries from operational records
- Viewing tool execution in the Runiq dashboard

## Why This Matters

LLMs are good at language, but business applications need controlled access to real data. Runiq lets a .NET application expose typed tools to agents, so agents can query structured business data instead of guessing from free text.

Typed tools make data access explicit, inspectable, and deterministic. The agent does not own the data layer, read the database directly, or invent records. It asks the host .NET application for data through a typed tool.

## Project Structure

```text
samples/
  Runiq.ExpenseDesk/
    Agents/
      ExpenseDataAnalyst.cs

    Data/
      ExpenseDeskDatabase.cs
      ExpenseDeskSeedData.cs

    Context/
      expense-policy.md

    Skills/
      expense-analysis.md

    Tools/
      ExpenseSearchTool.cs

    Program.cs
    README.md
    Runiq.ExpenseDesk.csproj
```

`Program.cs` wires the sample app, Runiq server, services, agent, and dashboard. The agent definition lives under `Agents`, the tool implementation lives under `Tools`, and the schema plus deterministic seed data live under `Data`.

`Context/expense-policy.md` and `Skills/expense-analysis.md` are sample assets. They are kept for future policy-aware examples, but the current Expense Data Analyst is a data-only agent.

## Agent: Expense Data Analyst

Expense Data Analyst is focused on expense data analysis. It answers questions about:

- monthly expenses
- yearly expenses
- highest expense
- highest spending employee
- highest spending department
- category totals
- department summaries
- employee summaries

When expense data is needed, it uses Expense Search.

## Tool: Expense Search

Expense Search is a typed .NET tool. It queries the in-memory SQLite database and supports these filters:

- `Year`
- `Month`
- `EmployeeId`
- `EmployeeName`
- `Department`
- `Category`

The tool returns joined expense and personnel rows, including:

- employee name
- department
- title
- location
- expense date
- category
- amount
- currency
- description

It also returns `Count` and `TotalAmount` for the filtered result.

## Data Model

The sample uses two logical tables:

- `Personnel`
- `Expenses`

`Personnel` contains:

- `EmployeeId`
- `FullName`
- `Department`
- `Title`
- `Location`

`Expenses` contains:

- `ExpenseId`
- `EmployeeId`
- `ExpenseDate`
- `Category`
- `Amount`
- `Currency`
- `Description`

All amounts are in TRY to keep the sample focused on agent/tool behavior, not currency conversion.

## Known Sample Facts

- January 2026 has 10 expense records.
- January 2026 total amount is 111,750 TRY.
- The largest January 2026 expense is Deniz Ozturk, Marketing, 32,000 TRY.
- The sample contains 2025 and 2026 data.
- The sample contains departments such as Sales, Engineering, Marketing, Finance, and Operations.
- The sample contains categories such as Travel, Meal, Software, Office, Marketing, and Training.

## Running The Sample

From the repository root:

```bash
cd samples/Runiq.ExpenseDesk
dotnet run
```

Then open:

```text
http://localhost:6186/dashboard
```

If ASP.NET Core starts the app on a different port, use the port printed in the console output.

## Configuration

The sample uses OpenAI through the existing Runiq agent provider setup. Configure the API key through `appsettings.Development.json`, user secrets, or environment variables, depending on your local preference.

Example `appsettings.Development.json` shape:

```json
{
  "OpenAI": {
    "ApiKey": "YOUR_OPENAI_API_KEY"
  }
}
```

Do not commit `appsettings.Development.json`.

Visual Studio debugging can use `Properties/launchSettings.json`, which sets `ASPNETCORE_ENVIRONMENT=Development` for this sample.

## Try These Prompts

English:

- Show January 2026 expenses and total amount.
- What was the largest expense in January 2026?
- Who spent the most in 2026 and what was the total?
- Which department spent the most in 2026 and what was the total?
- Group 2026 expenses by category.
- Group 2026 expenses by employee.
- Show 2026 Travel expenses.
- Summarize Sales department expenses for 2026.
- Show expenses for Mehmet Kaya in 2026.
- Show expenses for a department that does not exist.

Turkish:

- Ocak 2026 masraflarını getir ve toplam tutarı söyle.
- Ocak 2026'nın en büyük masrafı ne?
- 2026 yılında en fazla masraf yapan kişi kim ve toplam ne kadar masraf yapmış?
- 2026 yılında en çok harcama yapan departman hangisi ve toplam tutarı nedir?
- 2026 masraflarını kategori bazında grupla.
- 2026 masraflarını çalışan bazında grupla.
- 2026 Travel masraflarını göster.
- Sales departmanının 2026 masraflarını özetle.
- Mehmet Kaya'nın 2026 masraflarını listele ve toplamını söyle.
- Legal departmanının 2026 masraflarını göster.

## Expected Tool Behavior

For expense questions, the dashboard should show an Expense Search tool call. For normal data analysis prompts, the agent should not need policy context. The tool result should be the source of truth.

## Current Scope

This sample currently focuses on row-data search and analysis. It is intentionally not a full expense management system.

Possible extensions:

- Expense Summary tool for deterministic SQL aggregations
- Expense Policy Advisor agent
- Expense Policy Check tool
- Policy-aware analysis using `Context/expense-policy.md`
- Budget comparison examples

## Disclaimer

This sample uses synthetic demo data. It is not financial advice, accounting advice, or a production expense approval system.
