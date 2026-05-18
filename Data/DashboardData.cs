using System.Collections.Generic;

namespace BugalterProject.Data;

public sealed record DashboardData(
    decimal CurrentBudget,
    decimal TotalPayableDebts,
    decimal TotalReceivableDebts,
    decimal CurrentMonthExpenses,
    IReadOnlyList<DashboardObligation> UpcomingObligations,
    IReadOnlyList<DashboardCategoryTotal> CategoryTotals);
