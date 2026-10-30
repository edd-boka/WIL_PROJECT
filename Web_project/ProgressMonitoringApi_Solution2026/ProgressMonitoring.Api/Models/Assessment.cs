namespace ProgressMonitoring.Api.Models;

// One test/assignment/exam for a subject and a grade. Every learner in that
// grade is in the class; each learner's result is a Mark pointing back here.
public class Assessment
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string AssessmentType { get; set; } = string.Empty; // "Test", "Assignment", "Exam"
    public Guid SubjectId { get; set; }
    public string Grade { get; set; } = string.Empty;
    public Guid TeacherId { get; set; }
    public DateOnly DateOfAssessment { get; set; }
    public double Total { get; set; }
}
