namespace BugalterProject.Data;

public sealed record ExpenseCategory(int CategoryId, string CategoryName)
{
    public override string ToString() => CategoryName;
}
