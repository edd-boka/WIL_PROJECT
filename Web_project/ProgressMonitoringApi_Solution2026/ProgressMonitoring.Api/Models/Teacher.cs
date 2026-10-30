namespace ProgressMonitoring.Api.Models;

// A staff profile may exist before its owner creates a login.
public class Teacher
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string StaffId { get; set; } = string.Empty;
    public string? Email { get; set; }
}
