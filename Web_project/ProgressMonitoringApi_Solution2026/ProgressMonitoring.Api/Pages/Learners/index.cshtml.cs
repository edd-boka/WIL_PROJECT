using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProgressMonitoring.Api.Data;
using ProgressMonitoring.Api.Models;

namespace ProgressMonitoring.Api.Pages.Learners;

[Authorize(Roles = "teacher,admin")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db)
    {
        _db = db;
    }

    // ?grade=8&q=sam  (both optional, so the page is bookmarkable and works without JavaScript)
    [BindProperty(SupportsGet = true)] public string? Grade { get; set; }
    [BindProperty(SupportsGet = true, Name = "q")] public string? SearchTerm { get; set; }

    public record GradeCard(string Grade, int Count);

    public bool NeedsOnboarding { get; set; }
    public List<GradeCard> Cards { get; set; } = new();
    public int TotalLearners { get; set; }

    // The learners to show: empty until a grade is picked or a search is typed.
    public List<Learner> Learners { get; set; } = new();
    public bool HasSelection => !string.IsNullOrEmpty(Grade) || !string.IsNullOrWhiteSpace(SearchTerm);

    public async Task OnGetAsync()
    {
        var scope = await _db.GetScopeAsync(User);
        if (scope.NeedsOnboarding)
        {
            NeedsOnboarding = true;
            return;
        }

        // Only the grades this teacher teaches (admins see every grade).
        var grades = scope.VisibleGrades;
        // Only learners this teacher added (admins see all).
        var classLearners = await scope.VisibleLearners(_db).ToListAsync();

        // Alphabetical: surname, then first name (case-insensitive).
        var sorted = classLearners
            .OrderBy(l => l.LastName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(l => l.FirstName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        TotalLearners = sorted.Count;

        var counts = sorted.GroupBy(l => l.Grade).ToDictionary(g => g.Key, g => g.Count());
        Cards = grades
            .Select(g => new GradeCard(g, counts.GetValueOrDefault(g, 0)))
            .ToList();

        // Ignore a grade that isn't one of this teacher's.
        if (!string.IsNullOrEmpty(Grade) && !grades.Contains(Grade))
            Grade = null;

        IEnumerable<Learner> result = sorted;

        if (!string.IsNullOrEmpty(Grade))
            result = result.Where(l => l.Grade == Grade);

        var term = (SearchTerm ?? string.Empty).Replace(",", " ").Trim();
        if (term.Length > 0)
        {
            // Every word typed must appear in "First Last" or "Last First".
            var words = term.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            result = result.Where(l =>
            {
                var full = $"{l.FirstName} {l.LastName} {l.LastName} {l.FirstName}";
                return words.All(w => full.Contains(w, StringComparison.CurrentCultureIgnoreCase));
            });
        }

        if (HasSelection)
            Learners = result.ToList();
    }
}
