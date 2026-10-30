namespace ProgressMonitoring.Api.Models;

public class LearnerSubject

{
    public Guid LearnerId { get; set; }
    public Guid SubjectId { get; set; }
    public DateOnly EnrollmentDate { get; set; }

}
