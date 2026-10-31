using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProgressMonitoring.Api.Data;

namespace ProgressMonitoring.Api.Pages;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db)
    {
        _db = db;
    }

    public string TeacherName { get; set; } = "Teacher";
    public string? StaffId { get; set; }
    public bool HasTeacherProfile { get; set; }

    // Learners the logged-in teacher teaches (not everyone in the system).
    public int LearnerCount { get; set; }
    public int MarksCapturedByMe { get; set; }

    public async Task OnGetAsync()
    {
        var scope = await _db.GetScopeAsync(User);

        LearnerCount = await scope.VisibleLearners(_db).CountAsync();

        if (scope.TeacherId is Guid teacherId)
        {
            var teacher = await _db.Teachers.FirstOrDefaultAsync(t => t.Id == teacherId);
            if (teacher is not null)
            {
                HasTeacherProfile = true;
                TeacherName = $"{teacher.FirstName} {teacher.LastName}";
                StaffId = teacher.StaffId;
                MarksCapturedByMe = await _db.Marks.CountAsync(m => m.TeacherId == teacher.Id);
            }
        }
    }
}
