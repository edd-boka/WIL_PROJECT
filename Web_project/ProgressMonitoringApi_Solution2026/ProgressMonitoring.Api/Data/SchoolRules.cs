namespace ProgressMonitoring.Api.Data;

public static class SchoolRules
{
    public static readonly string[] Grades =
        { "1", "2", "3", "4", "5", "6", "7"};

    // Grades 1-3 are the Foundation Phase (class teacher, own subject list);
    // grades 4-7 are the Intermediate Phase (subject teachers).
    public static readonly string[] FoundationGrades = { "1", "2", "3" };
    public static readonly string[] IntermediateGrades = { "4", "5", "6", "7" };
    public static readonly string[] SubjectPhases = { "Foundation", "Intermediate", "Both" };

    public static bool IsFoundationGrade(string? grade) =>
        grade is not null && FoundationGrades.Contains(grade);

    public static string PhaseOfGrade(string? grade) =>
        IsFoundationGrade(grade) ? "Foundation" : "Intermediate";

    // Can a subject with this Phase be taught in this grade?
    public static bool SubjectFitsGrade(string? subjectPhase, string? grade) =>
        string.IsNullOrEmpty(subjectPhase) || subjectPhase == "Both" || subjectPhase == PhaseOfGrade(grade);

    // A mark at or above this percentage is a Pass. Change it here to change it everywhere.
    public const double PassPercent = 50;

    // Sorts grades numerically (R, 1, 2 ... 10, 11, 12) instead of as text.
    public static int GradeOrder(string? grade) =>
        grade == "R" ? 0 : (int.TryParse(grade, out var n) ? n : 99);

    public static double Percent(double score, double total) =>
        total > 0 ? score / total * 100 : 0;

    public static bool IsPass(double score, double total) =>
        total > 0 && Percent(score, total) >= PassPercent;
}
