using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ProgressMonitoring.Api.Models;

namespace ProgressMonitoring.Api.Data;

public record ClassMarkRow(Guid LearnerId, string LearnerName, double? Score);
public record ClassMarkInput(Guid LearnerId, double? Score);

// What the logged-in user is allowed to see: the subject + grade pairs the
// teacher chose when onboarding. Admins see everything.
public class TeacherScope
{
    public bool IsAdmin { get; init; }
    public Guid? TeacherId { get; init; }
    public List<TeacherSubject> Assignments { get; init; } = new();

    public bool NeedsOnboarding => !IsAdmin && Assignments.Count == 0;

    public bool Teaches(Guid subjectId, string grade) =>
        IsAdmin || Assignments.Any(a => a.SubjectId == subjectId && a.Grade == grade);

    public bool TeachesGrade(string grade) =>
        IsAdmin || Assignments.Any(a => a.Grade == grade);

    public List<string> VisibleGrades =>
        IsAdmin
            ? SchoolRules.Grades.ToList()
            : Assignments.Select(a => a.Grade).Distinct().OrderBy(SchoolRules.GradeOrder).ToList();

    // The learners this user may see. Admins see everyone; a teacher sees only the
    // learners they added themselves that sit in a grade they teach. Every page and
    // API endpoint that touches learners or their marks should start from this.
    public IQueryable<Learner> VisibleLearners(AppDbContext db)
    {
        if (IsAdmin) return db.Learners;
        if (TeacherId is not Guid tid) return db.Learners.Where(l => false);

        var grades = VisibleGrades;
        return db.Learners.Where(l => l.TeacherId == tid && grades.Contains(l.Grade));
    }

    // An assessment belongs to the teacher who created it. Other teachers - even ones
    // who teach the same subject and grade - cannot open it. Admins can open any.
    public bool CanAccessAssessment(Assessment a) =>
        IsAdmin || (TeacherId is Guid tid && a.TeacherId == tid && Teaches(a.SubjectId, a.Grade));
}

public static class AssessmentExtensions
{
    public static readonly string[] AssessmentTypes = { "Test", "Assignment", "Exam" };

    // Finds the Teacher profile for the logged-in user (User.Id comes from the login claim).
    public static async Task<Guid?> GetCurrentTeacherIdAsync(this AppDbContext db, ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        if (!Guid.TryParse(raw, out var userId)) return null;

        return await db.Teachers
            .Where(t => t.UserId == userId)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync();
    }

    public static async Task<TeacherScope> GetScopeAsync(this AppDbContext db, ClaimsPrincipal user)
    {
        var teacherId = await db.GetCurrentTeacherIdAsync(user);
        // Only assignments whose subject belongs to the grade's phase count. Leftover rows from
        // before phases existed (e.g. Grade 3 + Natural Sciences) are ignored everywhere:
        // filters, class lists, learners and marks.
        var assignments = new List<TeacherSubject>();
        if (teacherId is Guid tid)
        {
            var rows = await (
                from ts in db.TeacherSubjects
                join s in db.Subjects on ts.SubjectId equals s.Id
                where ts.TeacherId == tid
                select new { Assignment = ts, s.Phase }).ToListAsync();

            assignments = rows
                .Where(r => SchoolRules.SubjectFitsGrade(r.Phase, r.Assignment.Grade))
                .Select(r => r.Assignment)
                .ToList();
        }

        return new TeacherScope
        {
            IsAdmin = user.IsInRole("admin"),
            TeacherId = teacherId,
            Assignments = assignments
        };
    }

    // The assessment owner's learners in the assessment's grade, with their score if one exists.
    // Learners added by other teachers never appear here.
    public static async Task<List<ClassMarkRow>> GetClassMarksAsync(this AppDbContext db, Assessment a)
    {
        var learners = await db.Learners
            .Where(l => l.Grade == a.Grade && l.TeacherId == a.TeacherId)
            .OrderBy(l => l.LastName).ThenBy(l => l.FirstName)
            .ToListAsync();

        var scores = await db.Marks
            .Where(m => m.AssessmentId == a.Id)
            .ToDictionaryAsync(m => m.LearnerId, m => m.Score);

        return learners
            .Select(l => new ClassMarkRow(
                l.Id,
                $"{l.FirstName} {l.LastName}",
                scores.TryGetValue(l.Id, out var sc) ? (double?)sc : null))
            .ToList();
    }

    // Saves a whole class in one go. A null score removes that learner's mark.
    // Returns a list of error messages; an empty list means everything was saved.
    public static async Task<List<string>> SaveClassMarksAsync(
        this AppDbContext db, Assessment a, Guid teacherId, IEnumerable<ClassMarkInput> inputs)
    {
        var errors = new List<string>();

        var learners = await db.Learners
            .Where(l => l.Grade == a.Grade && l.TeacherId == a.TeacherId)
            .ToDictionaryAsync(l => l.Id);

        var existing = await db.Marks
            .Where(m => m.AssessmentId == a.Id)
            .ToDictionaryAsync(m => m.LearnerId);

        var rows = inputs.GroupBy(i => i.LearnerId).Select(g => g.Last()).ToList();

        foreach (var input in rows)
        {
            if (!learners.TryGetValue(input.LearnerId, out var learner))
            {
                errors.Add($"Learner {input.LearnerId} is not in this class (Grade {a.Grade}).");
                continue;
            }

            if (input.Score is double s && (s < 0 || s > a.Total))
                errors.Add($"{learner.FirstName} {learner.LastName}: score must be between 0 and {a.Total}.");
        }
        if (errors.Count > 0) return errors;

        foreach (var input in rows)
        {
            existing.TryGetValue(input.LearnerId, out var mark);

            if (input.Score is not double score)
            {
                if (mark is not null) db.Marks.Remove(mark);
                continue;
            }

            if (mark is null)
            {
                db.Marks.Add(new Mark
                {
                    Id = Guid.NewGuid(),
                    LearnerId = input.LearnerId,
                    SubjectId = a.SubjectId,
                    TeacherId = teacherId,
                    AssessmentType = a.AssessmentType,
                    Score = score,
                    Total = a.Total,
                    DateRecorded = DateTime.UtcNow,
                    AssessmentId = a.Id
                });
            }
            else
            {
                mark.Score = score;
                mark.Total = a.Total;
                mark.TeacherId = teacherId;
            }
        }

        await db.SaveChangesAsync();
        return errors;
    }
}
