using System;

namespace BugalterProject.Data;

public sealed record ExpenseItem(
    int ExpenseId,
    int UserId,
    string OwnerName,
    int CategoryId,
    string CategoryName,
    decimal Amount,
    string Description,
    DateTime ExpenseDate)
{
    public override string ToString() => $"{CategoryName} - {Amount}";
}
