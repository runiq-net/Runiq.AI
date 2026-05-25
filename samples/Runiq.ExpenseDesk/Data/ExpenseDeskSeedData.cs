namespace Runiq.ExpenseDesk.Data;

public static class ExpenseDeskSeedData
{
    public static IReadOnlyList<PersonnelSeedRow> Personnel { get; } =
    [
        new("E001", "Ayse Demir", "Sales", "Account Executive", "Istanbul"),
        new("E002", "Mehmet Kaya", "Sales", "Regional Sales Manager", "Ankara"),
        new("E003", "Elif Sahin", "Engineering", "Senior Software Engineer", "Istanbul"),
        new("E004", "Can Arslan", "Engineering", "Engineering Manager", "Izmir"),
        new("E005", "Zeynep Yilmaz", "Marketing", "Marketing Specialist", "Istanbul"),
        new("E006", "Burak Aydin", "Finance", "Finance Analyst", "Ankara"),
        new("E007", "Selin Koc", "Operations", "Operations Lead", "Bursa"),
        new("E008", "Deniz Ozturk", "Marketing", "Campaign Manager", "Izmir"),
        new("E009", "Murat Celik", "Operations", "Procurement Specialist", "Istanbul"),
    ];

    public static IReadOnlyList<ExpenseSeedRow> Expenses { get; } =
    [
        new("EXP-2025-001", "E001", "2025-01-08", "Travel", 8200m, "TRY", "Client visit flights"),
        new("EXP-2025-002", "E002", "2025-01-14", "Meal", 1850m, "TRY", "Partner lunch"),
        new("EXP-2025-003", "E003", "2025-01-20", "Software", 4300m, "TRY", "Developer tools renewal"),
        new("EXP-2025-004", "E005", "2025-02-04", "Marketing", 15500m, "TRY", "Local campaign materials"),
        new("EXP-2025-005", "E006", "2025-02-11", "Office", 2400m, "TRY", "Finance team supplies"),
        new("EXP-2025-006", "E007", "2025-02-18", "Travel", 9400m, "TRY", "Warehouse audit travel"),
        new("EXP-2025-007", "E008", "2025-03-03", "Training", 6200m, "TRY", "Marketing analytics course"),
        new("EXP-2025-008", "E004", "2025-03-16", "Software", 5100m, "TRY", "Architecture tool subscription"),
        new("EXP-2025-009", "E009", "2025-03-22", "Office", 3600m, "TRY", "Operations storage equipment"),
        new("EXP-2025-010", "E001", "2025-04-05", "Meal", 2100m, "TRY", "Customer dinner"),
        new("EXP-2025-011", "E003", "2025-04-13", "Travel", 7600m, "TRY", "Engineering conference travel"),
        new("EXP-2025-012", "E006", "2025-04-25", "Training", 4200m, "TRY", "Finance compliance workshop"),

        new("EXP-2026-001", "E001", "2026-01-05", "Travel", 12500m, "TRY", "Sales kickoff travel"),
        new("EXP-2026-002", "E002", "2026-01-07", "Meal", 2300m, "TRY", "Enterprise prospect dinner"),
        new("EXP-2026-003", "E003", "2026-01-09", "Software", 6800m, "TRY", "Cloud debugging suite"),
        new("EXP-2026-004", "E004", "2026-01-11", "Training", 8200m, "TRY", "Engineering leadership training"),
        new("EXP-2026-005", "E005", "2026-01-13", "Marketing", 18500m, "TRY", "January demand generation assets"),
        new("EXP-2026-006", "E006", "2026-01-16", "Office", 2800m, "TRY", "Finance monitors"),
        new("EXP-2026-007", "E007", "2026-01-18", "Travel", 9800m, "TRY", "Distribution center visit"),
        new("EXP-2026-008", "E008", "2026-01-21", "Marketing", 32000m, "TRY", "Major product launch campaign"),
        new("EXP-2026-009", "E009", "2026-01-24", "Office", 4100m, "TRY", "Operations shelving"),
        new("EXP-2026-010", "E002", "2026-01-28", "Travel", 14750m, "TRY", "Strategic client roadshow"),

        new("EXP-2026-011", "E001", "2026-02-03", "Meal", 1750m, "TRY", "Client breakfast"),
        new("EXP-2026-012", "E002", "2026-02-06", "Travel", 11800m, "TRY", "Regional partner meetings"),
        new("EXP-2026-013", "E003", "2026-02-09", "Software", 3900m, "TRY", "IDE licenses"),
        new("EXP-2026-014", "E004", "2026-02-12", "Software", 7200m, "TRY", "Incident analysis platform"),
        new("EXP-2026-015", "E005", "2026-02-15", "Marketing", 21500m, "TRY", "Paid social campaign"),
        new("EXP-2026-016", "E006", "2026-02-17", "Training", 5600m, "TRY", "Tax update seminar"),
        new("EXP-2026-017", "E007", "2026-02-20", "Office", 3300m, "TRY", "Operations floor equipment"),
        new("EXP-2026-018", "E008", "2026-02-22", "Travel", 10200m, "TRY", "Agency workshop travel"),
        new("EXP-2026-019", "E009", "2026-02-25", "Meal", 900m, "TRY", "Supplier meeting refreshments"),

        new("EXP-2026-020", "E001", "2026-03-02", "Travel", 9300m, "TRY", "Customer success visits"),
        new("EXP-2026-021", "E002", "2026-03-06", "Travel", 13200m, "TRY", "National sales tour"),
        new("EXP-2026-022", "E003", "2026-03-10", "Training", 7400m, "TRY", "Advanced systems course"),
        new("EXP-2026-023", "E004", "2026-03-14", "Software", 5600m, "TRY", "Load testing service"),
        new("EXP-2026-024", "E005", "2026-03-18", "Meal", 2450m, "TRY", "Agency planning dinner"),
        new("EXP-2026-025", "E006", "2026-03-20", "Office", 1900m, "TRY", "Finance filing equipment"),
        new("EXP-2026-026", "E007", "2026-03-23", "Travel", 10850m, "TRY", "Operations site inspection"),
        new("EXP-2026-027", "E008", "2026-03-27", "Marketing", 17400m, "TRY", "Retargeting campaign"),
        new("EXP-2026-028", "E009", "2026-03-30", "Training", 7900m, "TRY", "Procurement negotiation training"),

        new("EXP-2026-029", "E001", "2026-04-04", "Software", 5200m, "TRY", "Sales intelligence add-on"),
        new("EXP-2026-030", "E002", "2026-04-08", "Travel", 35800m, "TRY", "Executive account meetings"),
        new("EXP-2026-031", "E003", "2026-04-11", "Office", 2700m, "TRY", "Engineering lab supplies"),
        new("EXP-2026-032", "E004", "2026-04-15", "Meal", 1600m, "TRY", "Team planning lunch"),
        new("EXP-2026-033", "E005", "2026-04-18", "Marketing", 23800m, "TRY", "Spring campaign sponsorship"),
        new("EXP-2026-034", "E006", "2026-04-21", "Software", 4600m, "TRY", "Forecasting tool renewal"),
        new("EXP-2026-035", "E007", "2026-04-24", "Travel", 9700m, "TRY", "Logistics vendor review"),
        new("EXP-2026-036", "E008", "2026-04-27", "Training", 6800m, "TRY", "Brand strategy workshop"),
        new("EXP-2026-037", "E009", "2026-04-29", "Office", 2500m, "TRY", "Operations mobile devices"),
        new("EXP-2026-038", "E002", "2026-04-30", "Meal", 2600m, "TRY", "Quarter-end customer dinner"),
    ];
}

public sealed record PersonnelSeedRow(
    string EmployeeId,
    string FullName,
    string Department,
    string Title,
    string Location);

public sealed record ExpenseSeedRow(
    string ExpenseId,
    string EmployeeId,
    string ExpenseDate,
    string Category,
    decimal Amount,
    string Currency,
    string Description);
