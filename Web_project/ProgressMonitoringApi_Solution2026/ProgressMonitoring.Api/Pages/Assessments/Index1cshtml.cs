using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProgressMonitoring.Api.Data;
using ProgressMonitoring.Api.Models;

namespace ProgressMonitoring.Api.Pages.Assessments;

[Authorize(Roles = "teacher,admin")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public List<AssessmentRow> Items { get; set; } = new();
    public List<Subject> Subjects { get; set; } = new();

    // ?grade=5&subjectId=...&q=fractions  (all optional, so the page is bookmarkable)
    [BindProperty(SupportsGet = true)] public string? Grade { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? SubjectId { get; set; }
    [BindProperty(SupportsGet = true, Name = "q")] public string? SearchTerm { get; set; }

    public record GradeCard(string Grade, int Count);
    public record SubjectChip(Guid Id, string Name, int Count);

    public List<GradeCard> GradeCards { get; set; } = new();
    public List<SubjectChip> SubjectChips { get; set; } = new();
    public int TotalAssessments { get; set; }
    public bool HasFilter =>
        !string.IsNullOrEmpty(Grade) || SubjectId is not null || !string.IsNullOrWhiteSpace(SearchTerm);

    public class AssessmentRow
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public Guid SubjectId { get; set; }
        public string Grade { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public string AssessmentType { get; set; } = string.Empty;
        public DateOnly Date { get; set; }
        public double Total { get; set; }
        public int MarksCaptured { get; set; }
        // Learners in the assessment's grade (the same class Capture shows).
        public int LearnerCount { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        Subjects = await _db.Subjects.OrderBy(s => s.Name).ToListAsync();

        var scope = await _db.GetScopeAsync(User);
        var subjectNames = Subjects.ToDictionary(s => s.Id, s => s.Name);

        // A teacher only ever sees assessments they created; only admins see everyone's.
        var query = _db.Assessments.AsQueryable();
        if (!scope.IsAdmin)
        {
            var ownerId = scope.TeacherId ?? Guid.Empty;
            query = query.Where(a => a.TeacherId == ownerId);
        }

        var all = await (from a in query
                       join s in _db.Subjects on a.SubjectId equals s.Id
                       orderby a.DateOfAssessment descending
                       select new AssessmentRow
                       {
                           Id = a.Id,
                           Title = a.Title,
                           SubjectName = s.Name,
                           AssessmentType = a.AssessmentType,
                           Date = a.DateOfAssessment,
                           Grade = a.Grade,
                           SubjectId = a.SubjectId,
                           Total = a.Total,
                           MarksCaptured = _db.Marks.Count(m => m.AssessmentId == a.Id),
                           LearnerCount = _db.Learners.Count(l => l.Grade == a.Grade && l.TeacherId == a.TeacherId)
                       }).ToListAsync();

        TotalAssessments = all.Count;

        // ---- Filters: grade cards, subject chips and a search box ----
        var grades = scope.VisibleGrades;
        if (!string.IsNullOrEmpty(Grade) && !grades.Contains(Grade)) Grade = null;
        var gradeChosen = !string.IsNullOrEmpty(Grade);

        // Search matches the assessment title and/or the subject name; every word typed must match.
        var words = (SearchTerm ?? string.Empty).Replace(",", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var searched = all.Where(r => words.All(w =>
                r.Title.Contains(w, StringComparison.CurrentCultureIgnoreCase) ||
                r.SubjectName.Contains(w, StringComparison.CurrentCultureIgnoreCase)))
            .ToList();

        // Subject chips: the subjects this teacher teaches (narrowed to the chosen grade).
        var chipSubjectIds = scope.IsAdmin
            ? Subjects.Select(s => s.Id).ToList()
            : scope.Assignments
                .Where(x => !gradeChosen || x.Grade == Grade)
                .Select(x => x.SubjectId)
                .Distinct()
                .ToList();

        SubjectChips = chipSubjectIds
            .Where(subjectNames.ContainsKey)
            .Select(id => new SubjectChip(
                id,
                subjectNames[id],
                searched.Count(r => r.SubjectId == id && (!gradeChosen || r.Grade == Grade))))
            .OrderBy(c => c.Name)
            .ToList();

        if (SubjectId is not null && !SubjectChips.Any(c => c.Id == SubjectId)) SubjectId = null;

        // Grade cards: counts respect the subject and search, so they show what each grade would give.
        GradeCards = grades
            .Select(g => new GradeCard(
                g,
                searched.Count(r => r.Grade == g && (SubjectId is null || r.SubjectId == SubjectId))))
            .ToList();

        Items = searched
            .Where(r => (!gradeChosen || r.Grade == Grade) && (SubjectId is null || r.SubjectId == SubjectId))
            .ToList();
    }
}
