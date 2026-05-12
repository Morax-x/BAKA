namespace BugalterProject.Data;

public sealed record AuthResult(bool Success, string Message, AppUser? User);
