using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProgressMonitoring.Api.Data;
using ProgressMonitoring.Api.Models;

namespace ProgressMonitoring.Api.Pages.Learners;

[Authorize(Roles = "teacher,admin")]
public class AddModel : PageModel
{
    private readonly AppDbContext _db;
    public AddModel(AppDbContext db) => _db = db;

    public List<string> GradeOptions { get; set; } = new();

    [BindProperty] public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required, StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required, StringLength(20)]
        public string Grade { get; set; } = string.Empty;

        // Parents sign in to the parent app with these emails and are linked to this learner automatically.
        [Required(ErrorMessage = "Enter a parent or guardian email."), EmailAddress(ErrorMessage = "Enter a valid parent email."), StringLength(200)]
        public string ParentEmail { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Enter a valid second parent email."), StringLength(200)]
        public string? ParentEmail2 { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var scope = await _db.GetScopeAsync(User);
        if (scope.NeedsOnboarding) return RedirectToPage("/Teaching/Index");

        GradeOptions = scope.VisibleGrades;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(bool addAnother)
    {
        var scope = await _db.GetScopeAsync(User);
        if (scope.NeedsOnboarding) return RedirectToPage("/Teaching/Index");

        GradeOptions = scope.VisibleGrades;

        // The learner is stamped with the teacher who adds them.
        var ownerId = scope.TeacherId;
        if (!scope.IsAdmin && ownerId is null)
        {
            ModelState.AddModelError(string.Empty, "This account has no teacher profile, so it cannot add learners.");
            return Page();
        }

        var first = Input.FirstName.Trim();
        var last = Input.LastName.Trim();
        var grade = Input.Grade.Trim();

        // A teacher can only add learners to grades they teach.
        if (!GradeOptions.Contains(grade))
            ModelState.AddModelError("Input.Grade", "Choose one of the grades you teach.");

        if (ModelState.IsValid && await _db.Learners.AnyAsync(l =>
                l.TeacherId == ownerId &&
                l.FirstName.ToLower() == first.ToLower() &&
                l.LastName.ToLower() == last.ToLower() &&
                l.Grade == grade))
        {
            ModelState.AddModelError(string.Empty, $"{first} {last} (Grade {grade}) already exists.");
        }

        if (!ModelState.IsValid) return Page();

        var learner = new Learner
        {
            Id = Guid.NewGuid(),
            FirstName = first,
            LastName = last,
            Grade = grade,
            TeacherId = ownerId
        };
        _db.Learners.Add(learner);

        // Store the parent emails; a parent who already has an account is linked straight away,
        // and anyone who signs up later with one of these emails is linked when they do.
        await _db.AddParentEmailsAsync(learner.Id, new[] { Input.ParentEmail, Input.ParentEmail2 });
        await _db.SaveChangesAsync();

        TempData["Message"] = $"{first} {last} added to Grade {grade}.";
        return addAnother ? RedirectToPage() : RedirectToPage("Index");
    }
}
