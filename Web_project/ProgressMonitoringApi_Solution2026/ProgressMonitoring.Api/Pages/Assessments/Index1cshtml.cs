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

    [BindProperty] public string? NewTitle { get; set; }
    [BindProperty] public string? NewAssessmentType { get; set; }
    [BindProperty] public string? NewClass { get; set; }
    [BindProperty] public DateOnly NewDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    [BindProperty] public double NewTotal { get; set; }

    public List<AssessmentRow> Items { get; set; } = new();
    public List<Subject> Subjects { get; set; } = new();
    public List<CreateClassOption> CreateClassOptions { get; set; } = new();
    public bool CanCreateAssessment { get; set; }

    // ?grade=5&subjectId=...&q=fractions  (all optional, so the page is bookmarkable)
    [BindProperty(SupportsGet = true)] public string? Grade { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? SubjectId { get; set; }
    [BindProperty(SupportsGet = true, Name = "q")] public string? SearchTerm { get; set; }

    public record GradeCard(string Grade, int Count);
    public record SubjectChip(Guid Id, string Name, int Count);
    public record CreateClassOption(string Value, string Label);

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
        NewClass = CreateClassOptions.FirstOrDefault()?.Value;
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        var scope = await _db.GetScopeAsync(User);
        await LoadAsync();

        if (scope.TeacherId is null)
            ModelState.AddModelError(string.Empty, "This account has no teacher profile.");

        var title = (NewTitle ?? string.Empty).Trim();
        if (title.Length == 0)
            ModelState.AddModelError(nameof(NewTitle), "Enter an assessment title.");
        if (!AssessmentExtensions.AssessmentTypes.Contains(NewAssessmentType ?? string.Empty))
            ModelState.AddModelError(nameof(NewAssessmentType), "Choose Test, Assignment or Exam.");
        if (!double.IsFinite(NewTotal) || NewTotal <= 0)
            ModelState.AddModelError(nameof(NewTotal), "The total must be greater than zero.");

        var classOption = CreateClassOptions.FirstOrDefault(option => option.Value == NewClass);
        if (classOption is null)
        {
            ModelState.AddModelError(nameof(NewClass), "Choose a subject and grade you teach.");
        }

        if (!ModelState.IsValid)
            return Page();

        var parts = classOption!.Value.Split('|');
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var subjectId) ||
            scope.TeacherId is not Guid teacherId)
        {
            ModelState.AddModelError(nameof(NewClass), "Choose a valid subject and grade.");
            return Page();
        }

        var assessment = new Assessment
        {
            Id = Guid.NewGuid(),
            Title = title,
            AssessmentType = NewAssessmentType!,
            SubjectId = subjectId,
            Grade = parts[1],
            TeacherId = teacherId,
            DateOfAssessment = NewDate,
            Total = NewTotal
        };

        _db.Assessments.Add(assessment);
        await _db.SaveChangesAsync();

        TempData["Message"] = "Assessment created. You can now capture marks for the class.";
        return RedirectToPage("/Assessments/Index1");
    }

    private async Task LoadAsync()
    {
        Subjects = await _db.Subjects.OrderBy(s => s.Name).ToListAsync();

        var scope = await _db.GetScopeAsync(User);
        CanCreateAssessment = scope.TeacherId is not null;
        var subjectNames = Subjects.ToDictionary(s => s.Id, s => s.Name);
        var availableClasses = scope.IsAdmin
            ? (from subject in Subjects
               from grade in SchoolRules.Grades
               where SchoolRules.SubjectFitsGrade(subject.Phase, grade)
               select (subject.Id, Grade: grade))
            : scope.Assignments.Select(assignment => (Id: assignment.SubjectId, Grade: assignment.Grade));
        CreateClassOptions = availableClasses
            .Distinct()
            .Where(item => subjectNames.ContainsKey(item.Id))
            .OrderBy(item => SchoolRules.GradeOrder(item.Grade))
            .ThenBy(item => subjectNames[item.Id])
            .Select(item => new CreateClassOption(
                $"{item.Id}|{item.Grade}",
                $"Grade {item.Grade} — {subjectNames[item.Id]}"))
            .ToList();


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
