namespace ProgressMonitoring.Api.Models;

// A parent/guardian email the teacher entered when adding a learner. This is what links
// a parent to their children automatically: when someone signs up or signs in with this
// email, a ParentLearnerLink is created for every learner that lists it. A parent with
// several children simply appears on several learners; a learner can list more than one
// parent/guardian email. Emails are stored trimmed and lower-case.
public class LearnerParentEmail
{
    public Guid Id { get; set; }
    public Guid LearnerId { get; set; }
    public string Email { get; set; } = string.Empty;
}
