namespace ProgressMonitoring.Api.Models;

public class Subject
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty; // e.g. "Mathematics", "English"

    // Which phase teaches this subject: "Foundation" (Grades 1-3), "Intermediate"
    // (Grades 4-7) or "Both". Existing subjects default to "Both".
    public string Phase { get; set; } = "Both";
}
