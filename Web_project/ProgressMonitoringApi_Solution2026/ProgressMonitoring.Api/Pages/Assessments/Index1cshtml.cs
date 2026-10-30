using System.ComponentModel.DataAnnotations;
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

    // NEW — this is what was missing. Backs the "Class" dropdown: one
    // entry per subject+grade the teacher is actually assigned to.
    public List<ClassOption> MyClasses { get; set; } = new();

    [BindProperty] public InputModel Input { get; set; } = new();

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

    // NEW — one option in the "Class" dropdown.
    public record ClassOption(string Key, string Label);

    public class InputModel
    {
        [Required, StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string AssessmentType { get; set; } = "Test";

        [Required(ErrorMessage = "Choose a class.")]
        public string ClassKey { get; set; } = string.Empty;
        public DateOnly DateOfAssessment { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [Range(0.5, 10000)]
        public double Total { get; set; } = 100;
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();

        // Picking a grade and subject pre-selects that class in the "New Assessment" form.
        if (!string.IsNullOrEmpty(Grade) && SubjectId is Guid sid)
        {
            var key = $"{sid}|{Grade}";
            if (MyClasses.Any(c => c.Key == key)) Input.ClassKey = key;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var teacherId = await _db.GetCurrentTeacherIdAsync(User);
        if (teacherId is null)
            ModelState.AddModelError(string.Empty, "This account has no teacher profile, so it cannot create assessments.");

        var scope = await _db.GetScopeAsync(User);

        Guid subjectId = Guid.Empty;
        string grade = string.Empty;
        var parts = (Input.ClassKey ?? string.Empty).Split('|');

        if (parts.Length != 2 || !Guid.TryParse(parts[0], out subjectId))
        {
            ModelState.AddModelError("Input.ClassKey", "Choose a class.");
        }
        else
        {
            grade = parts[1];
            var isRealAssignment = scope.Assignments.Any(a => a.SubjectId == subjectId && a.Grade == grade);
            if (!isRealAssignment)
            {
                ModelState.AddModelError("Input.ClassKey", "That isn't one of your assigned classes.");
            }
        }

        if (!AssessmentExtensions.AssessmentTypes.Contains(Input.AssessmentType))
            ModelState.AddModelError("Input.AssessmentType", "Choose Test, Assignment or Exam.");

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var assessment = new Assessment
        {
            Id = Guid.NewGuid(),
            Title = Input.Title.Trim(),
            AssessmentType = Input.AssessmentType,
            SubjectId = subjectId,
            Grade = grade,
            TeacherId = teacherId!.Value,
            DateOfAssessment = Input.DateOfAssessment,
            Total = Input.Total
        };
        _db.Assessments.Add(assessment);
        await _db.SaveChangesAsync();

        return RedirectToPage("/Marks/Capture", new { id = assessment.Id });
    }

    private async Task LoadAsync()
    {
        Subjects = await _db.Subjects.OrderBy(s => s.Name).ToListAsync();

        var scope = await _db.GetScopeAsync(User);
        var subjectNames = Subjects.ToDictionary(s => s.Id, s => s.Name);

        // NEW — populates the dropdown from the teacher's real assignments.
        MyClasses = scope.Assignments
            .OrderBy(a => subjectNames.GetValueOrDefault(a.SubjectId, ""))
            .ThenBy(a => SchoolRules.GradeOrder(a.Grade))
            .Select(a => new ClassOption(
                $"{a.SubjectId}|{a.Grade}",
                $"{subjectNames.GetValueOrDefault(a.SubjectId, "Unknown subject")} — Grade {a.Grade}"))
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