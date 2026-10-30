namespace ProgressMonitoring.Api.Models;

public class Learner
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;

    // The teacher who added this learner. Only that teacher (and admins) can see
    // the learner and their marks/assessments. Null for learners created before
    // ownership existed - those are visible to admins only until assigned.
    public Guid? TeacherId { get; set; }
}
