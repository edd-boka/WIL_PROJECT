namespace ProgressMonitoring.Api.Models;

// Mirrors lib/models/mark.dart on the Flutter side — but note that Subject
// is a foreign key (SubjectId) here, not free text, and TeacherId records
// who captured the mark. If Flutter was built against an earlier free-text
// version, it needs updating to match — see the Flutter template's README.
public class Mark
{
    public Guid Id { get; set; }
    public Guid LearnerId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid TeacherId { get; set; }
    public string AssessmentType { get; set; } = string.Empty; // "Test", "Assignment", "Exam"
    public double Score { get; set; }
    public double Total { get; set; }
    public DateTime DateRecorded { get; set; }

    public Guid? AssessmentId { get; set; }


}


