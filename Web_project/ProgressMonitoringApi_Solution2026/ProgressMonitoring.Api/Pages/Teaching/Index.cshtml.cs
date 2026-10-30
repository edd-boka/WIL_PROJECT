using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProgressMonitoring.Api.Data;
using ProgressMonitoring.Api.Models;

namespace ProgressMonitoring.Api.Pages.Teaching;

// "My Classes". Two kinds of selection:
//  - Foundation Phase (Grades 1-3): a class teacher ticks the GRADES they teach and
//    automatically gets every Foundation subject for those grades.
//  - Intermediate Phase (Grades 4-7): a subject teacher ticks SUBJECT + GRADE pairs.
[Authorize(Roles = "teacher,admin")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public List<Subject> Subjects { get; set; } = new();
    public List<Subject> FoundationSubjects { get; set; } = new();
    public List<Subject> IntermediateSubjects { get; set; } = new();

    public HashSet<string> Ticked { get; set; } = new();           // "subjectId|grade" (grades 4-7)
    public HashSet<string> FoundationTicked { get; set; } = new(); // "1", "2", "3"

    public string ActivePhase { get; set; } = "foundation";        // which tab opens first
    public bool HasTeacherProfile { get; set; }
    public bool IsFirstTime { get; set; }

    public async Task OnGetAsync()
    {
        var scope = await _db.GetScopeAsync(User);
        HasTeacherProfile = scope.TeacherId is not null;
        IsFirstTime = HasTeacherProfile && scope.Assignments.Count == 0;

        Subjects = await _db.Subjects.OrderBy(s => s.Name).ToListAsync();
        FoundationSubjects = Subjects.Where(s => SchoolRules.SubjectFitsGrade(s.Phase, "1")).ToList();
        IntermediateSubjects = Subjects.Where(s => SchoolRules.SubjectFitsGrade(s.Phase, "4")).ToList();

        Ticked = scope.Assignments
            .Where(a => !SchoolRules.IsFoundationGrade(a.Grade))
            .Select(a => $"{a.SubjectId}|{a.Grade}")
            .ToHashSet();

        FoundationTicked = scope.Assignments
            .Where(a => SchoolRules.IsFoundationGrade(a.Grade))
            .Select(a => a.Grade)
            .ToHashSet();

        ActivePhase = scope.Assignments.Any(a => !SchoolRules.IsFoundationGrade(a.Grade))
            ? "intermediate"
            : "foundation";
    }

    public async Task<IActionResult> OnPostSaveAsync(List<string>? assignments, List<string>? foundationGrades)
    {
        var teacherId = await _db.GetCurrentTeacherIdAsync(User);
        if (teacherId is null)
        {
            TempData["Error"] = "This account has no teacher profile.";
            return RedirectToPage();
        }

        var subjects = await _db.Subjects.ToListAsync();
        var phaseById = subjects.ToDictionary(s => s.Id, s => s.Phase);
        var wanted = new HashSet<(Guid SubjectId, string Grade)>();

        // Intermediate Phase: subject + grade ticks (grades 4-7, subject must fit the phase).
        foreach (var raw in assignments ?? new List<string>())
        {
            var parts = raw.Split('|');
            if (parts.Length == 2 &&
                Guid.TryParse(parts[0], out var subjectId) &&
                phaseById.TryGetValue(subjectId, out var phase) &&
                SchoolRules.IntermediateGrades.Contains(parts[1]) &&
                SchoolRules.SubjectFitsGrade(phase, parts[1]))
            {
                wanted.Add((subjectId, parts[1]));
            }
        }

        // Foundation Phase: ticking a grade means "I am the class teacher" - every
        // Foundation subject for that grade is assigned.
        var foundationPicked = (foundationGrades ?? new List<string>())
            .Where(g => SchoolRules.FoundationGrades.Contains(g))
            .Distinct()
            .ToList();

        foreach (var grade in foundationPicked)
            foreach (var fitting in subjects.Where(x => SchoolRules.SubjectFitsGrade(x.Phase, grade)))
                wanted.Add((fitting.Id, grade));

        if (wanted.Count == 0)
        {
            TempData["Error"] = foundationPicked.Count > 0
                ? "There are no Foundation Phase subjects yet. Add them below, then save again."
                : "Tick at least one grade (Foundation) or subject and grade (Intermediate) that you teach.";
            return RedirectToPage();
        }

        var current = await _db.TeacherSubjects.Where(x => x.TeacherId == teacherId.Value).ToListAsync();
        var have = current.Select(c => (c.SubjectId, c.Grade)).ToHashSet();

        _db.TeacherSubjects.RemoveRange(current.Where(c => !wanted.Contains((c.SubjectId, c.Grade))));
        _db.TeacherSubjects.AddRange(wanted
            .Where(w => !have.Contains(w))
            .Select(w => new TeacherSubject
            {
                Id = Guid.NewGuid(),
                TeacherId = teacherId.Value,
                SubjectId = w.SubjectId,
                Grade = w.Grade,
                AcademicYear = DateTime.Today.Year
            }));
        await _db.SaveChangesAsync();

        TempData["Message"] = $"Saved. You teach {wanted.Count} class(es).";
        return RedirectToPage("/Learners/Index");
    }

    public async Task<IActionResult> OnPostAddSubjectAsync(string? newSubjectName, string? phase)
    {
        var name = (newSubjectName ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > 100)
        {
            TempData["Error"] = "Enter a subject name (up to 100 characters).";
            return RedirectToPage();
        }

        var chosenPhase = SchoolRules.SubjectPhases.Contains(phase ?? string.Empty) ? phase! : "Both";

        var exists = await _db.Subjects.AnyAsync(s => s.Name.ToLower() == name.ToLower());
        if (exists)
        {
            TempData["Error"] = $"{name} is already in the list.";
            return RedirectToPage();
        }

        var subject = new Subject { Id = Guid.NewGuid(), Name = name, Phase = chosenPhase };
        _db.Subjects.Add(subject);

        // A class teacher in Grades 1-3 gets a new Foundation subject for the grades they already teach.
        var teacherId = await _db.GetCurrentTeacherIdAsync(User);
        if (teacherId is Guid tid && chosenPhase != "Intermediate")
        {
            var foundationGrades = await _db.TeacherSubjects
                .Where(x => x.TeacherId == tid)
                .Select(x => x.Grade)
                .Distinct()
                .ToListAsync();

            foreach (var grade in foundationGrades.Where(SchoolRules.IsFoundationGrade))
            {
                _db.TeacherSubjects.Add(new TeacherSubject
                {
                    Id = Guid.NewGuid(),
                    TeacherId = tid,
                    SubjectId = subject.Id,
                    Grade = grade,
                    AcademicYear = DateTime.Today.Year
                });
            }
        }

        await _db.SaveChangesAsync();

        TempData["Message"] = $"Subject {name} added.";
        return RedirectToPage();
    }
}
