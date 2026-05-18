using System;

namespace BugalterProject.Data;

public sealed record DebtItem(
    int DebtId,
    int UserId,
    string OwnerName,
    string PersonName,
    string DebtType,
    decimal Amount,
    decimal OriginalAmount,
    DateTime DueDate,
    bool IsSettled,
    DateTime? SettledDate)
{
    public string DebtTypeLabel => DebtType == "mie_datoreaza" ? "Sunt datori mie" : "Sunt dator eu";

    public override string ToString() => $"{PersonName} - {Amount}";
}
