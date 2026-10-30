using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProgressMonitoring.Api.Data;
using ProgressMonitoring.Api.Models;

namespace ProgressMonitoring.Api.Controllers;

public record CreateAssessmentRequest(
    string Title, string AssessmentType, Guid SubjectId, string Grade, DateOnly DateOfAssessment, double Total);

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "teacher,admin")]
public class AssessmentsController : ControllerBase
{
    private readonly AppDbContext _db;
    public AssessmentsController(AppDbContext db) => _db = db;

    // GET /api/assessments  -> only the assessments this teacher created (admins see all)
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var scope = await _db.GetScopeAsync(User);
        var all = await _db.Assessments.OrderByDescending(a => a.DateOfAssessment).ToListAsync();
        return Ok(all.Where(a => scope.CanAccessAssessment(a)));
    }

    // POST /api/assessments
    [HttpPost]
    public async Task<IActionResult> Create(CreateAssessmentRequest req)
    {
        var scope = await _db.GetScopeAsync(User);
        if (scope.TeacherId is null) return StatusCode(403, "This account has no teacher profile.");

        if (string.IsNullOrWhiteSpace(req.Title)) return BadRequest("Title is required.");
        if (!AssessmentExtensions.AssessmentTypes.Contains(req.AssessmentType))
            return BadRequest("AssessmentType must be Test, Assignment or Exam.");
        if (!SchoolRules.Grades.Contains(req.Grade)) return BadRequest("Unknown grade.");
        if (req.Total <= 0) return BadRequest("Total must be greater than 0.");
        if (!await _db.Subjects.AnyAsync(s => s.Id == req.SubjectId))
            return BadRequest("Unknown subject.");
        if (!scope.Teaches(req.SubjectId, req.Grade))
            return StatusCode(403, "You do not teach that subject in that grade.");

        var assessment = new Assessment
        {
            Id = Guid.NewGuid(),
            Title = req.Title.Trim(),
            AssessmentType = req.AssessmentType,
            SubjectId = req.SubjectId,
            Grade = req.Grade,
            TeacherId = scope.TeacherId.Value,
            DateOfAssessment = req.DateOfAssessment,
            Total = req.Total
        };
        _db.Assessments.Add(assessment);
        await _db.SaveChangesAsync();
        return Ok(assessment);
    }

    // GET /api/assessments/{id}/marks  -> the grade's class list with any existing scores
    [HttpGet("{id}/marks")]
    public async Task<IActionResult> GetMarks(Guid id)
    {
        var scope = await _db.GetScopeAsync(User);
        var assessment = await _db.Assessments.FindAsync(id);
        if (assessment is null || !scope.CanAccessAssessment(assessment)) return NotFound();

        var rows = await _db.GetClassMarksAsync(assessment);
        return Ok(new { assessment, rows });
    }

    // PUT /api/assessments/{id}/marks  body: [{ "learnerId": "...", "score": 17.5 }, ...]
    // A null score removes that learner's mark.
    [HttpPut("{id}/marks")]
    public async Task<IActionResult> SaveMarks(Guid id, List<ClassMarkInput> marks)
    {
        var scope = await _db.GetScopeAsync(User);
        var assessment = await _db.Assessments.FindAsync(id);
        if (assessment is null || !scope.CanAccessAssessment(assessment)) return NotFound();
        if (scope.TeacherId is null) return StatusCode(403, "This account has no teacher profile.");

        var errors = await _db.SaveClassMarksAsync(assessment, scope.TeacherId.Value, marks);
        return errors.Count > 0 ? BadRequest(new { errors }) : NoContent();
    }
}

