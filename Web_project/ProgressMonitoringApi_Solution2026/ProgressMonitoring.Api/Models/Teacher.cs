namespace ProgressMonitoring.Api.Models;

// A teacher's profile, linked 1:1 to a login (User). Kept separate from
// User so teacher-specific fields (e.g. subjects taught, in future) don't
// clutter the login/auth table.
public class Teacher
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}
