using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProgressMonitoring.Api.Data;
using ProgressMonitoring.Api.Models;

namespace ProgressMonitoring.Api.Pages.Learners;

// Read-only: viewing a learner's marks. The page shows one card per subject the
// learner takes (set by their grade's phase); picking a card lists that subject's tests (ordered by the
// date the mark was captured) and the learner's average in that subject.
// Marks are only ever captured from the Assessments > Capture flow, not from here.
[Authorize(Roles = "teacher,admin")]
public class MarksModel : PageModel
{
    private readonly AppDbContext _db;

    public MarksModel(AppDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public Guid LearnerId { get; set; }

    // The subject card that was clicked (optional).
    [BindProperty(SupportsGet = true)]
    public Guid? SubjectId { get; set; }

    // "asc" = oldest first, anything else = newest first.
    [BindProperty(SupportsGet = true)]
    public string? Sort { get; set; }

    public Learner? SelectedLearner { get; set; }
    public List<SubjectCard> SubjectCards { get; set; } = new();
    public SubjectCard? SelectedSubject { get; set; }
    public double? OverallAverage { get; set; }

    // Tests for the selected subject only, ordered by DateRecorded.
    public List<MarkRow> Marks { get; set; } = new();

    public bool OldestFirst => string.Equals(Sort, "asc", StringComparison.OrdinalIgnoreCase);

    public record SubjectCard(Guid Id, string Name, bool IsGradeSubject, int TestCount, double? Average);

    public class MarkRow
    {
        public Guid SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public string AssessmentTitle { get; set; } = string.Empty;
        public string AssessmentType { get; set; } = string.Empty;
        public double ScoreValue { get; set; }
        public double Total { get; set; }
        public DateTime DateRecorded { get; set; }
    }

    public async Task OnGetAsync()
    {
        var scope = await _db.GetScopeAsync(User);
        var isAdmin = scope.IsAdmin;
        var teacherId = scope.TeacherId ?? Guid.Empty;

        // Another teacher's learner comes back as "not found" - no hint that it exists.
        SelectedLearner = await scope.VisibleLearners(_db).FirstOrDefaultAsync(l => l.Id == LearnerId);
        if (SelectedLearner is null) return;

        // Every mark this user may see for the learner.
        var allMarks = await (
            from m in _db.Marks
            join a in _db.Assessments on m.AssessmentId equals a.Id
            join s in _db.Subjects on a.SubjectId equals s.Id
            where m.LearnerId == LearnerId && (isAdmin || m.TeacherId == teacherId)
            select new MarkRow
            {
                SubjectId = s.Id,
                SubjectName = s.Name,
                AssessmentTitle = a.Title,
                AssessmentType = a.AssessmentType,
                ScoreValue = m.Score,
                Total = a.Total,
                DateRecorded = m.DateRecorded,
            }).ToListAsync();

        // A teacher only sees the subjects they teach for this learner's grade (Foundation class
        // teachers teach every Foundation subject; Intermediate teachers only their own subjects).
        // Marks in any other subject are never shown. Admins are not restricted.
        var grade = SelectedLearner.Grade;
        allMarks = allMarks.Where(r => scope.Teaches(r.SubjectId, grade)).ToList();

        // No manual enrolment: a learner's subjects come from their grade. A Foundation Phase
        // learner (Grades 1-3) takes every Foundation subject, an Intermediate Phase learner
        // (Grades 4-7) takes every Intermediate subject. Subjects added later appear automatically.
        var gradeSubjects = (await _db.Subjects.ToListAsync())
            .Where(s => SchoolRules.SubjectFitsGrade(s.Phase, grade) && scope.Teaches(s.Id, grade))
            .ToList();
        var gradeSubjectIds = gradeSubjects.Select(s => s.Id).ToHashSet();

        var cards = gradeSubjects
            .Select(s => MakeCard(s.Id, s.Name, true, allMarks))
            .ToList();

        // Marks that exist for a subject outside the learner's phase (for example captured before
        // phases existed) still get a card, tagged, so no captured mark is ever hidden.
        foreach (var group in allMarks.GroupBy(r => r.SubjectId).Where(x => !gradeSubjectIds.Contains(x.Key)))
            cards.Add(MakeCard(group.Key, group.First().SubjectName, false, allMarks));

        SubjectCards = cards.OrderByDescending(c => c.IsGradeSubject).ThenBy(c => c.Name).ToList();

        OverallAverage = allMarks.Count == 0
            ? (double?)null
            : allMarks.Average(r => SchoolRules.Percent(r.ScoreValue, r.Total));

        // The chosen subject: its tests, ordered by the date the mark was captured.
        SelectedSubject = SubjectCards.FirstOrDefault(c => c.Id == SubjectId);
        if (SelectedSubject is null)
        {
            SubjectId = null;
            return;
        }

        var subjectMarks = allMarks.Where(r => r.SubjectId == SelectedSubject.Id);
        Marks = OldestFirst
            ? subjectMarks.OrderBy(r => r.DateRecorded).ThenBy(r => r.AssessmentTitle).ToList()
            : subjectMarks.OrderByDescending(r => r.DateRecorded).ThenBy(r => r.AssessmentTitle).ToList();
    }

    // Average = the mean of the percentage for each test (same principle as before).
    private static SubjectCard MakeCard(Guid id, string name, bool isGradeSubject, List<MarkRow> allMarks)
    {
        var rows = allMarks.Where(r => r.SubjectId == id).ToList();
        double? average = rows.Count == 0
            ? (double?)null
            : rows.Average(r => SchoolRules.Percent(r.ScoreValue, r.Total));
        return new SubjectCard(id, name, isGradeSubject, rows.Count, average);
    }
}
