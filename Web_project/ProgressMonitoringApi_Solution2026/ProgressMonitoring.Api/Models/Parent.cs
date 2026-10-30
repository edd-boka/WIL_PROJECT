namespace ProgressMonitoring.Api.Models;

// A parent's profile, linked 1:1 to a login (User). A parent can be
// linked to one or more learners via ParentLearnerLink.
public class Parent
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    // True once the parent has entered the 6-digit code emailed to them. Children are only
    // linked (by the emails teachers entered) after this, so nobody can claim a child by
    // signing up with an email address they don't own.
    public bool EmailVerified { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}
