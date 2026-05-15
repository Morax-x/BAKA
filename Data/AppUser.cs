namespace BugalterProject.Data;

public sealed record AppUser(int UserId, string FirstName, string LastName, string Email, string Role)
{
    public bool IsAdmin => string.Equals(Role, "admin", System.StringComparison.OrdinalIgnoreCase);

    public override string ToString() => $"{FirstName} {LastName} ({Email})";
}
