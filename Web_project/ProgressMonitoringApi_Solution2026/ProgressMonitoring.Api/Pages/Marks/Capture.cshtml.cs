using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProgressMonitoring.Api.Data;
using ProgressMonitoring.Api.Models;

namespace ProgressMonitoring.Api.Pages.Assessments;

[Authorize(Roles = "teacher,admin")]
public class CaptureModel : PageModel
{
    private readonly AppDbContext _db;
    public CaptureModel(AppDbContext db) => _db = db;

    public Assessment? Assessment { get; set; }
    public string SubjectName { get; set; } = string.Empty;

    [BindProperty] public List<Row> Rows { get; set; } = new();

    public class Row
    {
        public Guid LearnerId { get; set; }
        public string LearnerName { get; set; } = string.Empty; // display only, never saved
        public double? Score { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var scope = await _db.GetScopeAsync(User);
        if (scope.NeedsOnboarding) return RedirectToPage("/Teaching/Index");
        if (!await LoadHeaderAsync(id, scope)) return NotFound();

        var classRows = await _db.GetClassMarksAsync(Assessment!);
        Rows = classRows
            .Select(r => new Row { LearnerId = r.LearnerId, LearnerName = r.LearnerName, Score = r.Score })
            .ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        var scope = await _db.GetScopeAsync(User);
        if (scope.NeedsOnboarding) return RedirectToPage("/Teaching/Index");
        if (!await LoadHeaderAsync(id, scope)) return NotFound();

        // A score that couldn't be read (e.g. "abc") must not be treated as blank,
        // because blank removes the learner's existing mark.
        if (!ModelState.IsValid) return Page();

        if (scope.TeacherId is null)
        {
            ModelState.AddModelError(string.Empty, "This account has no teacher profile, so it cannot capture marks.");
            return Page();
        }

        var errors = await _db.SaveClassMarksAsync(
            Assessment!, scope.TeacherId.Value, Rows.Select(r => new ClassMarkInput(r.LearnerId, r.Score)));

        if (errors.Count > 0)
        {
            foreach (var e in errors) ModelState.AddModelError(string.Empty, e);
            return Page();
        }

        TempData["Message"] = "Marks saved.";
        return RedirectToPage(new { id });
    }

    // Returns false when the assessment doesn't exist or isn't this teacher's own assessment.
    private async Task<bool> LoadHeaderAsync(Guid id, TeacherScope scope)
    {
        Assessment = await _db.Assessments.FindAsync(id);
        if (Assessment is null || !scope.CanAccessAssessment(Assessment)) return false;

        SubjectName = await _db.Subjects
            .Where(s => s.Id == Assessment.SubjectId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync() ?? string.Empty;
        return true;
    }
}
