using Runiq.ContextSpaces.Models.Sources;

namespace Runiq.ExpenseDesk.Context;

public static class ExpenseDeskContext
{
    public static ContextSpace Create()
    {
        return new ContextSpace(
                id: "expense-desk",
                name: "Expense Desk Context",
                description: "Sample expense analysis policy and skill documents.")
            .AddSources(sources => sources.FromFileSystem(
                id: "expense-policy",
                name: "Expense Policy",
                path: "./Context",
                description: "Static expense policy reference for future policy-aware samples."));
    }
}
