namespace ProgressMonitoring.Api.Models;

// Backs login. Role drives what a user can see/do — "teacher", "parent",
// or "admin" — matching the roles in your use case diagram.
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // "teacher" | "parent" | "admin"
}
