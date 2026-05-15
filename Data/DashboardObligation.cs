using System;

namespace BugalterProject.Data;

public sealed record DashboardObligation(string Title, string Subtitle, decimal Amount, DateTime DueDate);
