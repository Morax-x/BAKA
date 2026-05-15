using System;

namespace BugalterProject.Data;

public sealed record UtilityItem(
    int UtilityId,
    int UserId,
    string OwnerName,
    string UtilityName,
    decimal Amount,
    DateTime UtilityDate,
    bool IsPaid,
    DateTime? PaidDate)
{
    public override string ToString() => $"{UtilityName} - {Amount}";
}
