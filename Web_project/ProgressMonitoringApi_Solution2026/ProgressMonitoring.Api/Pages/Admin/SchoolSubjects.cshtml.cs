using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProgressMonitoring.Api.Data;
using ProgressMonitoring.Api.Models;

namespace ProgressMonitoring.Api.Pages.Admin;

[Authorize(Policy = "AdminUi")]
public class SchoolSubjectsModel : PageModel
{
    private readonly AppDbContext _db;

    public SchoolSubjectsModel(AppDbContext db) => _db = db;

    public List<Subject> Subjects { get; private set; } = new();

    [BindProperty]
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    [RegularExpression("^(Foundation|Intermediate|Both)$", ErrorMessage = "Choose a valid school phase.")]
    public string Phase { get; set; } = "Both";

    public async Task OnGetAsync() => await LoadSubjectsAsync();

    public async Task<IActionResult> OnPostAddAsync()
    {
        Name = Name.Trim();
        if (ModelState.IsValid &&
            await _db.Subjects.AnyAsync(subject => subject.Name.ToLower() == Name.ToLower()))
        {
            ModelState.AddModelError(nameof(Name), "A subject with this name already exists.");
        }

        if (!ModelState.IsValid)
        {
            await LoadSubjectsAsync();
            return Page();
        }

        _db.Subjects.Add(new Subject
        {
            Id = Guid.NewGuid(),
            Name = Name,
            Phase = Phase
        });
        await _db.SaveChangesAsync();

        TempData["AdminMessage"] = $"School subject “{Name}” added.";
        return RedirectToPage();
    }

    private async Task LoadSubjectsAsync()
    {
        Subjects = await _db.Subjects
            .OrderBy(subject => subject.Name)
            .ToListAsync();
    }
}
