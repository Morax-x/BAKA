using System.Collections.Generic;

namespace BugalterProject.Data;

public sealed record DashboardData(
    decimal CurrentBudget,
    decimal TotalDebts,
    decimal CurrentMonthExpenses,
    IReadOnlyList<DashboardObligation> UpcomingObligations,
    IReadOnlyList<DashboardCategoryTotal> CategoryTotals);
